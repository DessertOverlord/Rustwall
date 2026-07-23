using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Rustwall.ModSystems.RingedGenerator
{
    public class SeedDependentWorldGenParameters
    {
        public SeedDependentWorldGenParameters(ICoreServerAPI sapi, int seed, Dictionary<string, double> ringGeneratorWorldParameters)
        {
            World_Seed = seed;
            World_Params = ringGeneratorWorldParameters;

            ///Here we handle the stuff that GenMaps would normally handle
            var worldConfig = sapi.World.Config;
            LatitudeData latdata = new LatitudeData();
            float tempModifier = (float)ringGeneratorWorldParameters["globalTemperature"];
            float rainModifier = (float)ringGeneratorWorldParameters["globalPrecipitation"];
            float upheavelCommonness = (float)ringGeneratorWorldParameters["upheavelCommonness"];
            float landcover = (float)ringGeneratorWorldParameters["landcover"];
            float oceanscale = (float)ringGeneratorWorldParameters["oceanscale"];
            float landformScale = (float)ringGeneratorWorldParameters["landformScale"];
            latdata.polarEquatorDistance = worldConfig.GetString("polarEquatorDistance", "50000").ToInt(50000);
            NoiseClimate noiseClimate;

            string climate = worldConfig.GetString("worldClimate", "realistic");
            switch (climate)
            {
                case "realistic":
                    int spawnMinTemp = 6;
                    int spawnMaxTemp = 14;

                    string startingClimate = worldConfig.GetString("startingClimate");
                    switch (startingClimate)
                    {
                        case "hot":
                            spawnMinTemp = 28;
                            spawnMaxTemp = 32;
                            break;
                        case "warm":
                            spawnMinTemp = 19;
                            spawnMaxTemp = 23;
                            break;
                        case "cool":
                            spawnMinTemp = -5;
                            spawnMaxTemp = 1;
                            break;
                        case "icy":
                            spawnMinTemp = -15;
                            spawnMaxTemp = -10;
                            break;
                    }

                    noiseClimate = new NoiseClimateRealistic(
                        seed,
                        (double)sapi.WorldManager.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale,
                        latdata.polarEquatorDistance,
                        spawnMinTemp,
                        spawnMaxTemp
                        );
                    (noiseClimate as NoiseClimateRealistic).GeologicActivityStrength = (float)ringGeneratorWorldParameters["geologicActivity"];

                    latdata.isRealisticClimate = true;
                    latdata.ZOffset = (noiseClimate as NoiseClimateRealistic).ZOffset;
                    break;

                default:
                    noiseClimate = new NoiseClimatePatchy(seed);
                    break;
            }

            GenMaps mapGenerator = sapi.ModLoader.GetModSystem<GenMaps>();

            GenMaps_climateGen = GenMaps.GetClimateMapGen(seed + 1, noiseClimate);
            GenMaps_upheavelGen = GenMaps.GetGeoUpheavelMapGen(seed + 873, TerraGenConfig.geoUpheavelMapScale);

            /// this is a bit ugly and not accurate but GenMaps shits itself without this because
            /// requireLandAt is not defined in GenMaps at initialization, but I can't load any later
            /// because otherwise HandleRegionLoading is registered too late.
            ///
            /// REVIEW: This is probably not true any more because I am loading later in the process
            List<XZ> requireLandAt = new() { new XZ(0, 0) };

            GenMaps_oceanGen = GenMaps.GetOceanMapGen(seed + 1873, landcover, TerraGenConfig.oceanMapScale, oceanscale, requireLandAt, false);
            GenMaps_forestGen = GenMaps.GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            GenMaps_bushGen = GenMaps.GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            GenMaps_flowerGen = GenMaps.GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);
            GenMaps_beachGen = GenMaps.GetBeachMapGen(seed + 2273, TerraGenConfig.beachMapScale);
            GenMaps_geologicprovinceGen = GenMaps.GetGeologicProvinceMapGen(seed + 3, sapi);
            GenMaps_landformsGen = GenMaps.GetLandformMapGen(seed + 4, noiseClimate, sapi, landformScale);
        }

        //public string Ring_Name { get; set; }
        public Dictionary<string, double> World_Params { get; private set; }
        public int World_Seed { get; private set; }
        public MapLayerBase GenMaps_climateGen { get; private set; }
        public MapLayerBase GenMaps_upheavelGen { get; private set; }
        public MapLayerBase GenMaps_oceanGen { get; private set; }
        public MapLayerBase GenMaps_forestGen { get; private set; }
        public MapLayerBase GenMaps_bushGen { get; private set; }
        public MapLayerBase GenMaps_flowerGen { get; private set; }
        public MapLayerBase GenMaps_beachGen { get; private set; }
        public MapLayerBase GenMaps_geologicprovinceGen { get; private set; }
        public MapLayerBase GenMaps_landformsGen { get; private set; }

    }
}
