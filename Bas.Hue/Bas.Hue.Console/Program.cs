using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bas.Hue.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var bridge = await Bridge.DiscoverAsync();
            //var username = await bridge.RegisterAsync("testdevice");
            bridge.Username = "UrtLcUgQmzcyFpkVyVtFett4j1kaxY7sjEmqpdgf";

            var light = await bridge.GetLightAsync("1");
            await bridge.SetLightColorAsync(light, 127, 0.5f, 0.25f);
            var light2 = await bridge.GetLightAsync("1");

            await bridge.SetLightColorAsync(light, 255, 300, 1.0f);
            var light3 = await bridge.GetLightAsync("1");

        }
    }
}
