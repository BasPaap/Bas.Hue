using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using UPnP;

namespace Bas.Hue
{
    public sealed class Bridge
    {
        private Uri baseUri;
        public string Username { get; set; }
        private const string modelName = "Philips hue bridge";

        private static async Task<string> FindBridgeAddressAsync()
        {
            // Simultaneously try to find the bridge's address via UPNP and N-UPNP. As soon as a confirmed bridge address is returned, cancel the others and return.
            List<Task<string>> findAddressTasks = new List<Task<string>>();
            findAddressTasks.Add(FindAddressViaUpnpAsync());
            findAddressTasks.Add(FindAddressViaNupnpAsync());

            while (findAddressTasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(findAddressTasks); // Wait for the next Task to complete.
                findAddressTasks.Remove(finishedTask);

                // If the task has found a valid address, return it. Otherwise, we continue the loop and wait for the next task.
                var foundAddress = await finishedTask;
                if (!string.IsNullOrWhiteSpace(foundAddress))
                {
                    return foundAddress;
                }
            }

            return null;
        }

        private static async Task<string> FindAddressViaUpnpAsync()
        {
            var allUpnpDevices = await new Ssdp().SearchUPnPDevicesAsync("ssdp:all");
            var firstDevice = allUpnpDevices.FirstOrDefault(device => device.ModelName.StartsWith(Bridge.modelName, StringComparison.InvariantCultureIgnoreCase));

            Debug.WriteLine("Bridge found via UPnP");
            return firstDevice?.URLBase;
        }

        private static async Task<string> FindAddressViaNupnpAsync()
        {
            using (var httpClient = new HttpClient())
            {
                var devicesResponse = await httpClient.GetStringAsync("https://discovery.meethue.com/");
                var devices = JArray.Parse(devicesResponse);

                const string internalIpAddressKey = "internalipaddress";
                if (devices.Count > 0 && devices[0][internalIpAddressKey] != null)
                {
                    var bridgeAddress = $"http://{devices[0][internalIpAddressKey].Value<string>()}/";

                    var descriptionResponse = await httpClient.GetStringAsync($"{bridgeAddress}description.xml");
                    var description = XDocument.Parse(descriptionResponse);

                    XNamespace descriptionNamespace = "urn:schemas-upnp-org:device-1-0";
                    var modelNameElement = description.Element(descriptionNamespace + "root")?.Element(descriptionNamespace + "device")?.Element(descriptionNamespace + "modelName");
                    if (modelNameElement != null && ((string)modelNameElement).StartsWith(Bridge.modelName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Debug.WriteLine("Bridge found via N-UPnP");
                        return bridgeAddress;
                    }
                }
            }


            return null;
        }

        private async Task<string> SendApiCommandAsync(HttpMethod httpMethod, Operation operation, object content = null, bool isAnonymous = false)
        {
            // If this isn't an anonymous command, but there's no username, throw an exception.
            if (!isAnonymous && string.IsNullOrWhiteSpace(Username))
            {
                throw new InvalidOperationException();
            }

            // Execute the command. If something goes wrong, try to find the bridge again (maybe its IP address has changed?) and try again.
            const int maxRetries = 1;
            int numRetries = 0;
            while (numRetries <= maxRetries)
            {
                var httpResponse = await SendRequestAsync(httpMethod, Enum.GetName(typeof(Operation), operation).ToLower(), content != null ? JsonConvert.SerializeObject(content) : null);

                if (httpResponse.IsSuccessStatusCode)
                {
                    return await httpResponse.Content.ReadAsStringAsync();
                }

                var newBridge = await Bridge.DiscoverAsync();
                this.baseUri = newBridge.baseUri;
                numRetries++;
            };

            return null;
        }

        private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod httpMethod, string requestUri, string content)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.Method = httpMethod;
            
            if (requestUri != "none")
            {
                requestMessage.RequestUri = new Uri(requestUri, UriKind.Relative);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                requestMessage.Content = new StringContent(content);
            }

            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(this.baseUri, string.IsNullOrWhiteSpace(Username) ? $"api/" : $"api/{Username}/")
            };

            return await httpClient.SendAsync(requestMessage);
        }

        public static async Task<Bridge> DiscoverAsync()
        {
            var bridgeAddress = await FindBridgeAddressAsync();

            if (string.IsNullOrWhiteSpace(bridgeAddress))
            {
                return null;
            }

            return new Bridge(new Uri(bridgeAddress));
        }

        public async Task<string> RegisterAsync(string deviceName)
        {
            var content = new
            {
                devicetype = deviceName
            };

            var response = await SendApiCommandAsync(HttpMethod.Post, Operation.None, content, true);
            var result = JArray.Parse(response);

            if (result[0]["error"] != null)
            {
                return string.Empty;
            }

            return (string)result[0]["success"]["username"];
        }

        public Bridge(Uri baseUri)
        {
            this.baseUri = baseUri;
        }

        public Bridge(Uri baseUri, string username)
            : this(baseUri)
        {
            Username = username;
        }

        public async Task<IEnumerable<Light>> GetLightsAsync()
        {
            var response = await SendApiCommandAsync(HttpMethod.Get, Operation.Lights);

            var lightsDictionary = JsonConvert.DeserializeObject<Dictionary<string, Light>>(response);
            foreach (var key in lightsDictionary.Keys)
            {
                lightsDictionary[key].Id = key;
            }

            return lightsDictionary.Values;
        }
    }
}
