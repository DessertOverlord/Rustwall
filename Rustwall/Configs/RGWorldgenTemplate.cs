using Newtonsoft.Json;
using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Rustwall.Configs
{
    [ProtoContract]
    public class OreValues
    {
        /// <summary>
        /// Dictates how common ore is in an area (reflected by the propick's density mode).
        /// Uses bit positions 0x0000ff, values between 0 - 255
        /// </summary>
        [JsonProperty]
        [ProtoMember(1)]
        public int value;
        /// <summary>
        /// Unknown use at this time.
        /// Uses bit positions 0x00ff00
        /// </summary>
        [JsonProperty]
        [ProtoMember(2)]
        public int hypercommonness;
        /// <summary>
        /// No fucking clue what this does
        /// Uses bit positions 0xff0000
        /// </summary>
        [JsonProperty]
        [ProtoMember(3)]
        public int richness;
    }
    [ProtoContract]
    public class RGWorldgenTemplate 
    {
        /// <summary>
        /// Name of the ring template. Not really used other than for organization in the config.
        /// </summary>
        [JsonProperty]
        [ProtoMember(1)]
        public string Name;
        /// <summary>
        /// Ring number to start with. 0 is the safezone. Inclusive. Set this to the same as ToRing
        /// to modify only one ring.
        /// Using a negative number will cause the template to be ignored.
        /// I use this to show the default values and possible options.
        /// </summary>
        [JsonProperty]
        [ProtoMember(2)]
        public int FromRing;
        /// <summary>
        /// Ring number to end with. 0 is the safezone. Inclusive. Set this to the same as FromRing
        /// to modify only one ring.
        /// </summary>
        [JsonProperty]
        [ProtoMember(3)]
        public int ToRing;
        /// <summary>
        /// World seed to use for this ring.
        /// Only used if a dynamic worldgen template is used, to provide some randomness.
        /// If left unpopulated, a random one will be generated.
        /// </summary>
        [JsonProperty]
        [ProtoMember(4)]
        public int seed;
        /// The below fields are for "fill" type templates, which just fill all of the data for a region
        /// with that value
        /// -1 or null represent "unused" values, which will be ignored and not applied to the worldgen data.
        [JsonProperty]
        [ProtoMember(5)]
        public int beachData = -1;
        [JsonProperty]
        [ProtoMember(6)]
        public int biomeData = -1;
        [JsonProperty]
        [ProtoMember(7)]
        public int rainfallData = -1;
        [JsonProperty]
        [ProtoMember(8)]
        public int temperatureData = -1;
        [JsonProperty]
        [ProtoMember(9)]
        public int forestData = -1;
        [JsonProperty]
        [ProtoMember(10)]
        public int geoprovData = -1;
        [JsonProperty]
        [ProtoMember(11)]
        public string landformData = null;
        [JsonProperty]
        [ProtoMember(12)]
        public int oceanData = -1;
        [JsonProperty]
        [ProtoMember(13)]
        public Dictionary<string, OreValues> oreData;
        /// The fields below are for "dynamic" type templates, which use the default Vintage Story
        /// worldgen settings to modify the worldgen data for a region.
        [JsonProperty]
        [ProtoMember(14)]
        public double landformScale = 1;
        [JsonProperty]
        [ProtoMember(15)]
        public double globalTemperature = 1;
        [JsonProperty]
        [ProtoMember(16)]
        public double globalPrecipitation = 1;
        [JsonProperty]
        [ProtoMember(17)]
        public double globalForestation = 0;
        [JsonProperty]
        [ProtoMember(18)]
        public double landcover = 0.975;
        [JsonProperty]
        [ProtoMember(19)]
        public double oceanscale = 1;
        [JsonProperty]
        [ProtoMember(20)]
        public double upheavelCommonness = 0.3;
        [JsonProperty]
        [ProtoMember(21)]
        public double geologicActivity = 0.05;

        public int GetPackedClimateData()
        {
            int result = (rainfallData & 0xFF) << 8 | ((temperatureData & 0xFF) << 16);
            return result;
        }
    }
}