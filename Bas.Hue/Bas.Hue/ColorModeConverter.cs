﻿using Newtonsoft.Json;
using System;

namespace Bas.Hue
{
    sealed class ColorModeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.Value)
            {
                case "hs":
                    return ColorMode.HueAndSaturation;
                case "ct":
                    return ColorMode.ColorTemperature;
                case "xy":
                    return ColorMode.XY;
                default:
                    return ColorMode.None;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            switch ((ColorMode)value)
            {
                case ColorMode.HueAndSaturation:
                    writer.WriteValue("hs");
                    break;
                case ColorMode.XY:
                    writer.WriteValue("xy");
                    break;
                case ColorMode.ColorTemperature:
                    writer.WriteValue("ct");
                    break;
                case ColorMode.None:
                default:
                    break;
            }
        }
    }
}
