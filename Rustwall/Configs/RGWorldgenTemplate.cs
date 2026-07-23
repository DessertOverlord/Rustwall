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
        /// <summary>
        /// Name of the ring template. Not really used other than for organization in the config.
        /// </summary>
        [JsonProperty]
        public string Name;
        /// <summary>
        /// Ring number to start with. 0 is the safezone. Inclusive. Set this to the same as ToRing
        /// to modify only one ring.
        /// Using a negative number will cause the template to be ignored.
        /// I use this to show the default values and possible options.
        /// </summary>
        [JsonProperty]
        public int FromRing;
        /// <summary>
        /// Ring number to end with. 0 is the safezone. Inclusive. Set this to the same as FromRing
        /// to modify only one ring.
        /// </summary>
        [JsonProperty]
        public int ToRing;
        /// <summary>
        /// World seed to use for this ring.
        /// Only used if a dynamic worldgen template is used, to provide some randomness.
        /// If left unpopulated, a random one will be generated.
        /// </summary>
        [JsonProperty]
        public int seed;
        /// The below fields are for "fill" type templates, which just fill all of the data for a region
        /// with that value
        /// -1 or null represent "unused" values, which will be ignored and not applied to the worldgen data.
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
        public string landformData = null;
        [JsonProperty]
        public int oceanData = -1;
        [JsonProperty]
        public Dictionary<string, OreValues> oreData;
        /// The fields below are for "dynamic" type templates, which use the default Vintage Story
        /// worldgen settings to modify the worldgen data for a region.
        [JsonProperty]
        public double landformScale = 1;
        [JsonProperty]
        public double globalTemperature = 1;
        [JsonProperty]
        public double globalPrecipitation = 1;
        [JsonProperty]
        public double globalForestation = 0;
        [JsonProperty]
        public double landcover = 0.975;
        [JsonProperty]
        public double oceanscale = 1;
        [JsonProperty]
        public double upheavelCommonness = 0.3;
        [JsonProperty]
        public double geologicActivity = 0.05;

        public int GetPackedClimateData()
        {
            int result = (rainfallData & 0xFF) << 8 | ((temperatureData & 0xFF) << 16);
            return result;
        }
    }
}