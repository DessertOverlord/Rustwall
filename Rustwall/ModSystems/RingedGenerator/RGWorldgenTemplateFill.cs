using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rustwall.ModSystems.RingedGenerator
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RGWorldgenTemplateFill : RGWorldgenTemplate
    {
        [JsonProperty]
        public int beachData;
        [JsonProperty]
        public int biomeData;
        [JsonProperty]
        public int rainfallData;
        [JsonProperty]
        public int temperatureData;
        [JsonProperty]
        public int forestData;
        [JsonProperty]
        public int geoprovData;
        [JsonProperty]
        public string landformData;
    }
}