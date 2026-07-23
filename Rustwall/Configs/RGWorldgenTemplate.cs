using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Rustwall.Configs
{
    public class OreValues
    {
        /// <summary>
        /// Dictates how common ore is in an area (reflected by the propick's density mode).
        /// Uses bit positions 0x0000ff
        /// </summary>
        [JsonProperty]
        public int value;
        /// <summary>
        /// Unknown use at this time.
        /// Uses bit positions 0x00ff00
        /// </summary>
        [JsonProperty]
        public int hypercommonness;
        /// <summary>
        /// Current hypothesis: quality of individual discs?
        /// Uses bit positions 0xff0000
        /// </summary>
        [JsonProperty]
        public int richness;
    }
    public class RGWorldgenTemplate 
    {
        [JsonProperty]
        public string Name = "Default Ring Name";
        [JsonProperty]
        public int FromRing;
        [JsonProperty]
        public int ToRing;
        [JsonProperty]
        public int beachData = -1;
        [JsonProperty]
        public int biomeData = -1;
        [JsonProperty]
        public int rainfallData = -1;
        [JsonProperty]
        public int temperatureData = -1;
        [JsonProperty]
        public int forestData = -1;
        [JsonProperty]
        public int geoprovData = -1;
        [JsonProperty]
        public string landformData = "";
        [JsonProperty]
        public int oceanData = -1;
        [JsonProperty]
        public Dictionary<string, OreValues> oreData;
    }
}