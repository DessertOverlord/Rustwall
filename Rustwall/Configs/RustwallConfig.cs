using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static readonly Dictionary<string, double> RandomSettings = new()
        {

        };

        public static readonly Dictionary<string, double> ExampleDefaultWorldConfigSettings = new()
        {
            { "item1", 0.5 }
        };

        public static readonly Dictionary<string, double> WarmOcean = new()
        { 
        };

        public static readonly Dictionary<string, double> ColdOcean = new()
        { 
        };

        public static readonly Dictionary<string, double> RichMountains = new()
        { 
        };

        public static readonly Dictionary<string, double> FertilePlains = new()
        { 
        };

        public static readonly Dictionary<string, double> DenseForest = new()
        { 
        };

        public static readonly Dictionary<string, double> ExtremeHills = new()
        { 
        };

        //keep in mind that ring 0 is the safezone and is ignored anyways
        //these are in order, ring 0, ring 1, etc.
        //leaving emptiness at the end will result in randomization
        public readonly List<Dictionary<string, double>> RingTemplate = new()
        {
            ExampleDefaultWorldConfigSettings,
            FertilePlains,
            FertilePlains,
            FertilePlains,
            FertilePlains,
            DenseForest,
            DenseForest,
            DenseForest,
            DenseForest,
            WarmOcean,
            WarmOcean,
            WarmOcean,
            WarmOcean,
            ExtremeHills,
            ExtremeHills,
            ExtremeHills,
            ExtremeHills,
            ColdOcean,
            ColdOcean,
            ColdOcean,
            ColdOcean,
            RichMountains,
            RichMountains,
            RichMountains,
            RichMountains
        };
    }
}
