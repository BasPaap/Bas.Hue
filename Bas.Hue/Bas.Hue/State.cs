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

        [JsonProperty("hue")]
        public ushort Hue { get; set; }

        [JsonProperty("sat")]
        public byte Saturation { get; set; }
        
        [JsonProperty("ct")]
        public ushort ColorTemperature { get; set; }

        [JsonProperty("reachable")]
        public bool IsReachable { get; set; }

        [JsonProperty("alert")]
        public Alert Alert { get; set; }

        [JsonProperty("effect")]
        public Effect Effect { get; set; }

        [JsonProperty("colormode")]
        [JsonConverter(typeof(ColorModeConverter))]
        public ColorMode ColorMode { get; set; }
        
        [JsonIgnore]
        public float X { get; set; }

        [JsonIgnore]
        public float Y { get; set; }

        [JsonProperty("xy")]
        public float[] XY
        {
            get
            {
                return new[] { X, Y };
            }

            set
            {
                X = value[0];
                Y = value[1];
            }
        }


        [JsonProperty("transitiontime")]
        public int TransitionTimeInSeconds { get; set; }

        [JsonProperty("bri_inc")]
        public int BrightnessIncrement { get; set; }

        [JsonProperty("sat_inc")]
        public int SaturationIncrement { get; set; }

        [JsonProperty("hue_inc")]
        public int HueIncrement { get; set; }

        [JsonProperty("ct_inc")]
        public int ColorTemperatureIncrement { get; set; }

        [JsonIgnore]
        public float XIncrement { get; set; }

        [JsonIgnore]
        public float YIncrement { get; set; }

        [JsonProperty("xy_inc")]
        public float[] XYIncrement
        {
            get
            {
                return new[] { XIncrement, YIncrement };
            }

            set
            {
                XIncrement = value[0];
                YIncrement = value[1];
            }
        }
    }
}