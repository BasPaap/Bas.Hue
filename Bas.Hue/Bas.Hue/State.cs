using Newtonsoft.Json;
using System.Globalization;

namespace Bas.Hue
{
    public sealed class State
    {
        [JsonProperty("on")]
        public bool IsOn { get; set; }

        [JsonProperty("bri")]
        public byte Brightness { get; set; }
        public ushort Hue { get; set; }

        [JsonProperty("sat")]
        public byte Saturation { get; set; }
        
        [JsonProperty("ct")]
        public ushort ColorTemperature { get; set; }

        [JsonProperty("reachable")]
        public bool IsReachable { get; set; }

        public Alert Alert { get; set; }
        public Effect Effect { get; set; }

        [JsonConverter(typeof(ColorModeConverter))]
        public ColorMode ColorMode { get; set; }
        
        public float X { get; set; }
        public float Y { get; set; }

        public float[] XY
        {
            set
            {
                X = value[0];
                Y = value[1];
            }
        }

    }
}