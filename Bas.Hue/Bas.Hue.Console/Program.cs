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

            light.State.IsOn = false;
            var succeeded = await bridge.SetLightAsync(light.Id, new { on = true, hue = 0 });
        }
    }
}
