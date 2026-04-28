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

        //RingedGenerator options
        public int ringWidth = 1;
        public static readonly Dictionary<string, double> DefaultWorldConfigSettings = new()
        {
                { "landformScale", 1 },
                { "globalTemperature", 1 },
                { "globalPrecipitation", 1 },
                { "globalForestation", 0 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 0.3 },
                { "geologicActivity", 0.05 }
        };

        //keep in mind that ring 0 is the safezone and is ignored anyways
        //these are in order, ring 0, ring 1, etc.
        //leaving emptiness at the end will result in randomization
        //keywords:
        //"repeat", [int] -- repeat these settings this many times (for instance, repeat:3 would make a total of 4 rings)
        //"random", [int] -- randomize this ring using the int as an identifier for EnumDistribution.
            /*
            UNIFORM = 0
            Select completely random numbers within avg-var until avg+var
            
            TRIANGLE = 1
            Select random numbers with numbers near avg being the most commonly selected ones, following a triangle curve

            GAUSSIAN = 2
            Select random numbers with numbers near avg being the more commonly selected ones, following a gaussian curve
            
            NARROWGAUSSIAN = 3
            Select random numbers with numbers near avg being the much more commonly selected ones, following a narrow gaussian curve

            INVERSEGAUSSIAN = 4
            Select random numbers with numbers near avg being the less commonly selected ones, following an upside down gaussian curve

            NARROWINVERSEGAUSSIAN = 5
            Select random numbers with numbers near avg being the much less commonly selected ones, following an upside down gaussian curve

            INVEXP = 6
            Select random numbers in the form of avg + var, with numbers near avg being preferred

            STRONGINVEXP = 7
            Select random numbers in the form of avg + var, with numbers near avg being strongly preferred

            STRONGERINVEXP = 8
            Select random numbers in the form of avg + var, with numbers near avg being very strongly preferred

            DIRAC = 9
            Select completely random numbers within avg-var until avg+var only ONCE and then always 0

            VERYNARROWGAUSSIAN = 10
            Select random numbers with numbers near avg being the much much more commonly selected ones, following an even narrower gaussian curve
            */
        //"name-[string]", 0 -- identifier for the area. The number at the end is meaningless.
        //omitting any worldconfig option will have it use the default.
        public readonly List<Dictionary<string, double>> RingTemplates = new()
        {
            new Dictionary<string, double> 
            {
                { "name-safezone", 1 },
                { "landformScale", 1 },
                { "globalTemperature", 1 },
                { "globalPrecipitation", 1 },
                { "globalForestation", 0 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 0.3 },
                { "geologicActivity", 0.05 }
            },
            new Dictionary<string, double> 
            {
                { "name-fertile_plains", 1 },
                { "repeat", 3 },
                { "landformScale", 3 },
                { "globalTemperature", 1.5 },
                { "globalPrecipitation", 5 },
                { "globalForestation", -1 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 0 },
                { "geologicActivity", 0 }
            },
            new Dictionary<string, double> 
            {
                { "name-dense_forest", 1 },
                { "repeat", 3 },
                { "landformScale", 1.0 },
                { "globalTemperature", 1 },
                { "globalPrecipitation", 1 },
                { "globalForestation", 1 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 0 },
                { "geologicActivity", 0 }
            },
            new Dictionary<string, double> 
            {
                { "name-warm_ocean", 1 },
                { "repeat", 3 },
                { "landformScale", 1.0 },
                { "globalTemperature", 2 },
                { "globalPrecipitation", 4 },
                { "globalForestation", 1 },
                { "landcover", 0.20 },
                { "oceanscale", 2 },
                { "upheavelCommonness", 0 },
                { "geologicActivity", 0 }
            },
            new Dictionary<string, double> 
            {
                { "name-extreme_hills", 1 },
                { "repeat", 3 },
                { "landformScale", 0.5 },
                { "globalTemperature", 1 },
                { "globalPrecipitation", 1 },
                { "globalForestation", 0 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 1.0 },
                { "geologicActivity", 0.20 }
            },
            new Dictionary<string, double> 
            {
                { "name-cold_ocean", 1 },
                { "repeat", 3 },
                { "landformScale", 1.0 },
                { "globalTemperature", 0.25 },
                { "globalPrecipitation", 4 },
                { "globalForestation", 1 },
                { "landcover", 0.05 },
                { "oceanscale", 2 },
                { "upheavelCommonness", 0 },
                { "geologicActivity", 0 }
            },
            new Dictionary<string, double> 
            {
                { "name-rich_mountains", 1 },
                { "repeat", 3 },
                { "landformScale", 0.5 },
                { "globalTemperature", 1 },
                { "globalPrecipitation", 1 },
                { "globalForestation", 0 },
                { "landcover", 0.975 },
                { "oceanscale", 1 },
                { "upheavelCommonness", 1.0 },
                { "geologicActivity", 0.40 }
            },
        };
    }
}
