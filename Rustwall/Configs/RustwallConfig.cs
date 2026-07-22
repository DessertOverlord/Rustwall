using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Rustwall.Configs
{
    public class RustwallConfig
    {
        //TemporalStorm options
        public double TemporalStormDaysRemovedPerKill = 0.0417;

        //GlobalStability options
        public double DaysBetweenStormScoring = 30.0;
        public double DaysBeforeTheGreatDecay = 180.0;
        public double TemporalStormDamageMultiplier = 5.0;
        public int ChanceToBreakSimple = 288;
        public int ChanceToBreakComplex = 1440;

        //RebuildableBlock options
        public double GracePeriodDurationRepairOneStage = 0.2;
        public double GracePeriodDurationRepairFully = 1.0;

        //RingedGenerator options
        public int ringWidth = 2;
        public int safeZoneSize = 1;
        /*public static readonly Dictionary<string, double> DefaultWorldConfigSettings = new()
        {
                { "landformScale", 1 },
                { "globalTemperature", 1 },
                { "globalPrecipitation", 1 },
                { "globalForestation", 0 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 0.3 },
                { "geologicActivity", 0.05 }
        };*/

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public readonly List<RGWorldgenTemplate> RingTemplates =
        [
            new RGWorldgenTemplate
            {
                Name = "Safezone",
                FromRing = 0,
                ToRing = 0,
                rainfallData = 120,
                temperatureData = 100,
                forestData = 0,
                landformData = "realisticflatlands",
                oceanData = 0,
            },
            new RGWorldgenTemplate
            {
                Name = "Warm Fertile Plains",
                FromRing = 1,
                ToRing = 2,
                rainfallData = 120,
                temperatureData = 200,
                forestData = 0,
                landformData = "realisticflatlands",
                oceanData = 0,
            },
            new RGWorldgenTemplate
            {
                Name = "Dense Forest",
                FromRing = 3,
                ToRing = 4,
                rainfallData = 100,
                temperatureData = 100,
                forestData = 255,
                landformData = "realisticflatlands",
                oceanData = 0,
            },
            new RGWorldgenTemplate
            {
                Name = "Warm Ocean",
                FromRing = 3,
                ToRing = 4,
                rainfallData = 100,
                temperatureData = 160,
                forestData = 0,
                landformData = "realisticflatlands",
                oceanData = 255,
            },
            new RGWorldgenTemplate
            {
                Name = "Extreme Arctic Wasteland",
                FromRing = 5,
                ToRing = 6,
                rainfallData = 255,
                temperatureData = 0,
                forestData = 0,
                landformData = "humongous mountain, cavernless",
                oceanData = 0,
            },
            new RGWorldgenTemplate
            {
                Name = "Arctic Ocean",
                FromRing = 7,
                ToRing = 8,
                rainfallData = 255,
                temperatureData = 0,
                forestData = 0,
                landformData = "",
                oceanData = 255,
            },
            new RGWorldgenTemplate
            {
                Name = "Dense Jungle",
                FromRing = 9,
                ToRing = 10,
                rainfallData = 255,
                temperatureData = 190,
                forestData = 255,
                landformData = "realisticflatlands",
                oceanData = 0,
            },
            new RGWorldgenTemplate
            {
                Name = "Rich Mountains",
                FromRing = 11,
                ToRing = 12,
                rainfallData = 120,
                temperatureData = 80,
                forestData = 0,
                landformData = "humongous mountain, cavernless",
                oceanData = 0,
            },
        ];
    }
}
