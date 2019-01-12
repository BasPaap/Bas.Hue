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

            var lights = await bridge.GetLightsAsync();
        }
    }
}
