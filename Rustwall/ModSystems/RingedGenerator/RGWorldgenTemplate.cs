using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace Rustwall.ModSystems.RingedGenerator
{
    /*public enum EnumRingDefType 
    {
        //Fully random, just do whatever / may have a distribution tied to it
        Random,
        //Use worldgen values to create a simulated world with those parameters.
        // Provides some control compared to Random, but is not as exact as Fixed
        Dynamic,
        //Provide exact values to fill the entire region with. For instance, 
        // using 255 as a value for forests will guarentee forest literally everywhere.
        FixedFill,
        //Some mixture of Dynamic and FixedFill values.
        Hybrid
    }*/
    [JsonObject(MemberSerialization.OptIn)]
    public class RGWorldgenTemplate 
    {
        [JsonProperty]
        public AssetLocation Code = "defaulttemplate";
        [JsonProperty]
        public string Name = "Default Ring Name";
        [JsonProperty]
        public int FromRing;
        [JsonProperty]
        public int ToRing;
    }
}