using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
