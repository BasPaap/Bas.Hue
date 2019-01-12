using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bas.Hue
{
    
    public sealed class Light
    {   
        public string Id { get; set; }

        public State State { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string ModelId { get; set; }
        public string UniqueId { get; set; }
        public string ManufacturerName { get; set; }
        
        [JsonProperty("swversion")]
        public string SoftwareVersion { get; set; }
    }
}
