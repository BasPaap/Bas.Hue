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

        private async Task<string> SendApiCommandAsync(HttpMethod httpMethod, string operation, object content = null, bool isAnonymous = false)
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
                var httpResponse = await SendRequestAsync(httpMethod, operation, content != null ? JsonConvert.SerializeObject(content) : null);

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

            var response = await SendApiCommandAsync(HttpMethod.Post, string.Empty, content, true);
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
            var response = await SendApiCommandAsync(HttpMethod.Get, "lights");

            var lightsDictionary = JsonConvert.DeserializeObject<Dictionary<string, Light>>(response);
            foreach (var key in lightsDictionary.Keys)
            {
                lightsDictionary[key].Id = key;
            }

            return lightsDictionary.Values;
        }
        
        public async Task<Light> GetLightAsync(string id)
        {
            var response = await SendApiCommandAsync(HttpMethod.Get, $"lights/{id}");
            var light = JsonConvert.DeserializeObject<Light>(response);
            light.Id = id;

            return light;
        }

        public async Task SetLightStateAsync(Light light, object state)
        {
            var response = await SendApiCommandAsync(HttpMethod.Put, $"lights/{light.Id}/state", state);
            UpdateLightState(light, response);
        }

        public async Task TurnLightOnAsync(Light light)
        {
            var response = await SendApiCommandAsync(HttpMethod.Put, $"lights/{light.Id}/state", new { on = true });
            UpdateLightState(light, response);
        }

        public async Task TurnLightOffAsync(Light light)
        {
            var response = await SendApiCommandAsync(HttpMethod.Put, $"lights/{light.Id}/state", new { on = false });
            UpdateLightState(light, response);
        }

        /// <summary>
        /// Sets the color of a light via the color temperature value.
        /// </summary>
        /// <param name="light">The light to set</param>
        /// <param name="brightness">The brightness to set the light to. This is a value between 1 and 254.</param>
        /// <param name="colorTemperature">The Mired color temperature to set the light to. This is a value betweem 153 and 500.</param>
        /// <param name="transitionTimeInSeconds">Optional transition time in seconds.</param>
        /// <returns></returns>
        public async Task SetLightColorAsync(Light light, byte brightness, ushort colorTemperature, float transitionTimeInSeconds = 0)
        {
            var response = await SendApiCommandAsync(HttpMethod.Put, $"lights/{light.Id}/state", new
            {
                bri = GetValidBrightness(brightness),
                ct = Clamp(colorTemperature, 153, 500),
                transitiontime = GetTransitionTime(transitionTimeInSeconds)
            });
            UpdateLightState(light, response);
            light.State.ColorMode = ColorMode.ColorTemperature;
        }

        /// <summary>
        /// Set the color of a light via the saturation and hue values.
        /// </summary>
        /// <param name="light">The light to set</param>
        /// <param name="brightness">The brightness to set the light to. This is a value between 1 and 254.</param>
        /// <param name="saturation">The saturation to set the light to. This is a value between 0 and 254.</param>
        /// <param name="hue">The hue to set the light to. This is a value between 0 and 65535. Both values are red, 25500 is green and 46920 is blue.</param>
        /// <param name="transitionTimeInSeconds">Optional transition time in seconds.</param>
        /// <returns></returns>
        public async Task SetLightColorAsync(Light light, byte brightness, byte saturation, ushort hue, float transitionTimeInSeconds = 0)
        {
            var response = await SendApiCommandAsync(HttpMethod.Put, $"lights/{light.Id}/state", new
            {
                bri = GetValidBrightness(brightness),
                sat = Clamp(saturation, (byte)0, (byte)254),
                hue = Clamp(hue, 0, 65535),
                transitiontime = GetTransitionTime(transitionTimeInSeconds)
            });
            UpdateLightState(light, response);
            light.State.ColorMode = ColorMode.HueAndSaturation;
        }


        /// <summary>
        /// Set the color of a light via the coordinates in CIE color space.
        /// </summary>
        /// <param name="light">The light to set</param>
        /// <param name="brightness">The brightness to set the light to. This is a value between 1 and 254.</param>
        /// <param name="x">The x coordinate of the color in CIE color space. This is a value between 0.0f and 1.0f.</param>
        /// <param name="x">The y coordinate of the color in CIE color space. This is a value between 0.0f and 1.0f.</param>
        /// <param name="transitionTimeInSeconds">Optional transition time in seconds.</param>
        /// <returns></returns>
        public async Task SetLightColorAsync(Light light, byte brightness, float x, float y, float transitionTimeInSeconds = 0)
        {
            var response = await SendApiCommandAsync(HttpMethod.Put, $"lights/{light.Id}/state", new
            {
                bri = GetValidBrightness(brightness),
                xy = new [] { Clamp(x, 0.0f, 1.0f), Clamp(y, 0.0f, 1.0f) },
                transitiontime = GetTransitionTime(transitionTimeInSeconds)
            });
            UpdateLightState(light, response);
            light.State.ColorMode = ColorMode.XY;
        }

        private void UpdateLightState(Light light, string response)
        {
            var responseArray = JArray.Parse(response);
            string propertyPrefix = $"/lights/{light.Id}/state/";

            foreach (var item in responseArray)
            {
                if (item["success"] != null && ((JProperty)item["success"].First).Name.StartsWith(propertyPrefix))
                {   
                    switch (((JProperty)item["success"].First).Name.Substring(propertyPrefix.Length))
                    {
                        case "bri":
                            light.State.Brightness = ((JProperty)item["success"].First).ToObject<byte>();
                            break;
                        case "sat":
                            light.State.Saturation = ((JProperty)item["success"].First).ToObject<byte>();
                            break;
                        case "hue":
                            light.State.Hue = ((JProperty)item["success"].First).ToObject<ushort>();
                            break;
                        case "xy":
                            light.State.X = ((JProperty)item["success"].First).First[0].ToObject<float>();
                            light.State.Y = ((JProperty)item["success"].First).First[1].ToObject<float>();
                            break;
                        case "ct":
                            light.State.ColorTemperature = ((JProperty)item["success"].First).ToObject<ushort>();
                            break;
                        case "on":
                            light.State.IsOn = ((JProperty)item["success"].First).ToObject<bool>();
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private int GetTransitionTime(float transitionTimeInSeconds)
        {
            return Convert.ToInt32(transitionTimeInSeconds * 10);
        }

        private byte GetValidBrightness(byte brightness)
        {
            return Clamp(brightness, (byte)1, (byte)254);
        }

        private T Clamp<T>(T value, T minValue, T maxValue) where T: IComparable<T>
        {
            if (value.CompareTo(minValue) < 0)
            {
                return minValue;
            }
            else if (value.CompareTo(maxValue) > 0)
            {
                return maxValue;
            }
            else
            {
                return value;
            }
        }
    }
}
