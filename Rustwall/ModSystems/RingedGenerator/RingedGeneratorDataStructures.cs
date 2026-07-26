using Cairo;
using ProtoBuf;
using Rustwall.Configs;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Rustwall.ModSystems.RingedGenerator
{
    /// <summary>
    /// When we initialize RingData, we'll immediately initialize the RegionMapLayerGenerators, 
    /// which will generate all of the GenMaps map generators for this ring.
    /// Note that we do NOT initialize the MapRegionData here, because that is dependent 
    /// on the region coordinates and the map region itself, which we won't have access to until
    /// the region is generating.
    /// </summary>
    /// <param name="sapi"></param>
    /// <param name="seed"></param>
    /// <param name="template"></param>
    public class RingData
    {
        public RingData(ICoreServerAPI sapi, int seed, RGWorldgenTemplate template)
        {
            regionMapLayerGenerators = new RegionMapLayerGenerators(sapi, seed, template);
            this.template = template;
        }
        /// <summary>
        /// Gets the region map layer generators for this ring.
        /// </summary>
        public RegionMapLayerGenerators regionMapLayerGenerators { get; set; }
        /// <summary>
        /// Template used to make this ring. Stored for handy access.
        /// </summary>
        public RGWorldgenTemplate template { get; set; } 
    }

    public class RegionMapLayerGenerators
    {
        public RegionMapLayerGenerators(ICoreServerAPI sapi, int seed, RGWorldgenTemplate template)
        {
            var worldConfig = sapi.World.Config;
            LatitudeData latdata = new LatitudeData();
            float tempModifier = (float)template.globalTemperature;
            float rainModifier = (float)template.globalPrecipitation;
            float upheavelCommonness = (float)template.upheavelCommonness;
            float landcover = (float)template.landcover;
            float oceanscale = (float)template.oceanscale;
            float landformScale = (float)template.landformScale;
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
                    (noiseClimate as NoiseClimateRealistic).GeologicActivityStrength = (float)template.geologicActivity;

                    latdata.isRealisticClimate = true;
                    latdata.ZOffset = (noiseClimate as NoiseClimateRealistic).ZOffset;
                    break;

                default:
                    noiseClimate = new NoiseClimatePatchy(seed);
                    break;
            }

            //GenMaps mapGenerator = sapi.ModLoader.GetModSystem<GenMaps>();

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
