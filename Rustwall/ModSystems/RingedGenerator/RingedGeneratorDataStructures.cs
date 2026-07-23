using Cairo;
using Rustwall.Configs;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Rustwall.ModSystems.RingedGenerator
{
    public class RingData
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
        public RingData(ICoreServerAPI sapi, int seed, RGWorldgenTemplate template)
        {
            regionMapLayerGenerators = new RegionMapLayerGenerators(sapi, seed, template);
            this.template = template;
        }
        /// <summary>
        /// Gets the region map layer generators for this ring.
        /// </summary>
        public RegionMapLayerGenerators regionMapLayerGenerators;
        /// <summary>
        /// Gets the map region data for this ring. This is generated when the region is generating, 
        /// and is dependent on the region coordinates and the map region itself.
        /// </summary>
        public MapRegionData mapRegionData;
        /// <summary>
        /// Template used to make this ring. Stored for handy access.
        /// </summary>
        public RGWorldgenTemplate template { get; private set; }

        public MapRegionData InitMapRegionData(int regionX, int regionZ, IMapRegion mapRegion)
        {
            mapRegionData = new MapRegionData(regionX, regionZ, mapRegion, regionMapLayerGenerators, template);
            return mapRegionData;
        }
    }
    public class RegionMapLayerGenerators
    {
        public RegionMapLayerGenerators(ICoreServerAPI sapi, int seed, RGWorldgenTemplate template)
        {
            //World_Seed = seed;
            //World_Params = ringGeneratorWorldParameters;

            ///Here we handle the stuff that GenMaps would normally handle
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

        //public string Ring_Name { get; set; }
        //public Dictionary<string, double> World_Params { get; private set; }
        //public int World_Seed { get; private set; }
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

    public class MapRegionData
    {
        public MapRegionData(int regionX, int regionZ, IMapRegion mapRegion, RegionMapLayerGenerators inputParams, RGWorldgenTemplate template)
        {
            if (template.geoprovData > -1)
            {
                geoprovData = new int[mapRegion.GeologicProvinceMap.Size * mapRegion.GeologicProvinceMap.Size];
                geoprovData.Fill(template.geoprovData);
            } 
            else
            {
                geoprovData = inputParams.GenMaps_geologicprovinceGen.GenLayer(
                    regionX * mapRegion.GeologicProvinceMap.Size,
                    regionZ * mapRegion.GeologicProvinceMap.Size,
                    mapRegion.GeologicProvinceMap.Size,
                    mapRegion.GeologicProvinceMap.Size
                );
            }

            /// Need to correct this to account for differing rainfall / temp values
            if (template.climateData > -1)
            {
                climateData = new int[mapRegion.ClimateMap.Size * mapRegion.ClimateMap.Size];
                climateData.Fill(template.climateData);
            }
            else
            {
                climateData = inputParams.GenMaps_climateGen.GenLayer(
                    regionX * mapRegion.ClimateMap.Size,
                    regionZ * mapRegion.ClimateMap.Size,
                    mapRegion.ClimateMap.Size,
                    mapRegion.ClimateMap.Size
                );
            }

            /// This needs some edits... should not be putting in the current ClimateMap, we need to use the modified one.
            if (template.forestData > -1)
            {
                forestData = new int[mapRegion.ForestMap.Size * mapRegion.ForestMap.Size];
                forestData.Fill(template.forestData);
            }
            else
            {
                inputParams.GenMaps_forestGen.SetInputMap(mapRegion.ClimateMap, mapRegion.ForestMap);
                forestData = inputParams.GenMaps_forestGen.GenLayer(
                    regionX * mapRegion.ForestMap.Size, 
                    regionZ * mapRegion.ForestMap.Size, 
                    mapRegion.ForestMap.Size, 
                    mapRegion.ForestMap.Size
                );
            }


            /// Template doesn't contain upheavel data right now.
            /*if (template.upheavelData > -1)
            {
                upheavelData = new int[mapRegion.UpheavelMap.Size * mapRegion.UpheavelMap.Size];
                upheavelData.Fill(template.upheavelData);
            }
            else
            {
                upheavelData = inputParams.GenMaps_upheavelGen.GenLayer(
                    regionX * mapRegion.UpheavelMap.Size,
                    regionZ * mapRegion.UpheavelMap.Size, 
                    mapRegion.UpheavelMap.Size, 
                    mapRegion.UpheavelMap.Size
                );
            }*/

            if (template.oceanData > -1)
            {
                oceanData = new int[mapRegion.OceanMap.Size * mapRegion.OceanMap.Size];
                oceanData.Fill(template.oceanData);
            }
            else
            {
                oceanData = inputParams.GenMaps_oceanGen.GenLayer(
                    regionX * mapRegion.OceanMap.Size,
                    regionZ * mapRegion.OceanMap.Size,
                    mapRegion.OceanMap.Size,
                    mapRegion.OceanMap.Size
                );
            }

            if (template.beachData > -1)
            {
                beachData = new int[mapRegion.BeachMap.Size * mapRegion.BeachMap.Size];
                beachData.Fill(template.beachData);
            }
            else
            {
                beachData = inputParams.GenMaps_beachGen.GenLayer(
                    regionX * mapRegion.BeachMap.Size,
                    regionZ * mapRegion.BeachMap.Size,
                    mapRegion.BeachMap.Size,
                    mapRegion.BeachMap.Size
                );
            }

            /// Same as above, need to use the modified ClimateMap, not the current one.
            /// Not currently using bushGen anyways
            // inputParams.GenMaps_bushGen.SetInputMap(mapRegion.ClimateMap, mapRegion.ShrubMap);
            // = bushGen.GenLayer(regionX * noiseSizeShrubs, regionZ * noiseSizeShrubs, noiseSizeShrubs + 1, noiseSizeShrubs + 1);

            /// Unimplemented right now, skipping
            //flowerGen.SetInputMap(mapRegion.ClimateMap, mapRegion.BiomeMap);
            //mapRegion.BiomeMap.Data = flowerGen.GenLayer(regionX * noiseSizeForest, regionZ * noiseSizeForest, noiseSizeForest + 1, noiseSizeForest + 1);


            //pad = TerraGenConfig.landformMapPadding;

            if (template.landformData is not null && template.landformData != "")
            {
                int[] newLandformData = new int[mapRegion.LandformMap.Size * mapRegion.LandformMap.Size];
                string desiredLandform = template.landformData;
                int landformCode = NoiseLandforms.landforms.GetIndexByCode(desiredLandform);
                if (landformCode != -1)
                {
                    newLandformData.Fill(landformCode);
                    landformData = newLandformData;
                }
                else
                {
                    //sapi.Logger.Error($"Failed to find landform code for {desiredLandform}. Landform map will be unaltered.");
                }
                
            } 
            else
            {
                landformData = inputParams.GenMaps_landformsGen.GenLayer(
                    regionX * mapRegion.LandformMap.Size,
                    regionZ * mapRegion.LandformMap.Size,
                    mapRegion.LandformMap.Size,
                    mapRegion.LandformMap.Size);    
            }


            mapRegion.DirtyForSaving = true;
        } 
    
        public int[] beachData { get; private set; }
        public int[] biomeData { get; private set; }
        //public int[] rainfallData { get; private set; }
        //public int[] temperatureData { get; private set; }
        public int[] climateData { get; private set; }
        public int[] forestData { get; private set; }
        public int[] geoprovData { get; private set; }
        public int[] landformData { get; private set; }
        public int[] oceanData { get; private set; }
        public int[] upheavelData { get; private set; }
    }
}
