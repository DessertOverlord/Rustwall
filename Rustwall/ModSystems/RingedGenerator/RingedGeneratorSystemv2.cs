using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;
using VSSurvivalMod;

namespace Rustwall.ModSystems.RingedGenerator
{
    internal class RingedGeneratorSystemv2 : RustwallModSystem
    {
        // ringsize must be an even number (? haven't tried an odd number yet) and determines how wide each ring is.
        private static int ringSize = 2;
        // each dictionary holds the parameters for one ring's worldgen. organized into a list for scalability
        private List<Dictionary<string, double>> ringDictList { get; set; }
        // seedlist performs the same thing as above, just holding all of the seeds.
        private List<int> seedList { get; set; } = new List<int>();
        // this list is all of the settings we want to mess with. Can be added to easily.
        private readonly List<string> WorldgenParamsToScramble = new List<string> { "landformScale", "globalTemperature", "globalPrecipitation", "globalForestation", "landcover", "oceanscale", "upheavelCommonness", "geologicActivity" };
        // The default parameters for each of the associated parameters to scramble. ORDER MATTERS!
        // Some day I won't have to do this, but I haven't figured out how to gather the currently selected params until
        // after the game is saved for the first time.
        // TODO: programmatically gather the selected worldgen params on first launch.
        //private readonly List<double> WorldgenDefaultParams = new List<double> { 1, 1, 1, 0, 1, 1, 0.3, 0.05 };
        private readonly List<double> WorldgenDefaultParams = new List<double> { 1, 1, 1, 0, 0, 1, 0.3, 0.05 };
        private static int curRing = 0;
        private static int desiredRing = 0;
        private static int ringMapSize;
        private double regionMidPoint;




        protected override void RustwallStartServerSide()
        {
            GenMaps genmaps = sapi.ModLoader.GetModSystem<GenMaps>();

            /*
            //if (sapi.WorldManager.SaveGame.IsNew == true) { sapi.Server.ShutDown(); }


            //RegisterChatCommands();
            // This calculates map size relative to the resolution of the rings
            // It also checks to make sure the world is a square; if it is rectangular, the ring generator doesn't initialize
            //ringMapSize = sapi.WorldManager.MapSizeX == sapi.WorldManager.MapSizeZ ? (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) / 2 : -500;
            //regionMidPoint = ((ringMapSize + ringMapSize - 1) / 2.0);
            //ringDictList = new List<Dictionary<string, double>>(ringMapSize);

            //sapi.Event.SaveGameLoaded += InitRingedWorldGenerator;

            //InitRingedWorldGenerator();

            //Add the region method to the MapRegionGeneration event, causing it to be called any time the engine wants to generate a new region
            //MapRegionGeneratorDelegate regionHandler = HandleRegionLoading;
            //sapi.Event.MapRegionGeneration(regionHandler, "standard");

            //Add the chunk method to MapChunkGeneration; this is triggered any time a new chunk column is requested.
            //MapChunkGeneratorDelegate chunkHandler = HandleChunkLoading;
            //sapi.Event.MapChunkGeneration(chunkHandler, "standard");
            */

            //RegisterChatCommands();

            
        
        
        
        
        }
        /*
        private void RegisterChatCommands()
        {
            sapi.ChatCommands.Create("genv2")
                .WithDescription("info")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .WithArgs()
                .HandleWith((args) =>
                {
                    IWorldGenHandler WorldGenHandlerList = sapi.Event.GetRegisteredWorldGenHandlers("standard");

                    List<MapRegionGeneratorDelegate> MapRegGenDel = WorldGenHandlerList.OnMapRegionGen; 

                    foreach (var del in MapRegGenDel)
                    {

                    }

                    return TextCommandResult.Success("SOMETHING");
                });


        }

        
        #region Params stolen from GenMaps.cs

        public MapLayerBase upheavelGen;
        public MapLayerBase oceanGen;
        public MapLayerBase climateGen;
        public MapLayerBase flowerGen;
        public MapLayerBase bushGen;
        public MapLayerBase forestGen;
        public MapLayerBase beachGen;
        public MapLayerBase geologicprovinceGen;
        public MapLayerBase landformsGen;

        public int noiseSizeUpheavel;
        public int noiseSizeOcean;
        public int noiseSizeClimate;
        public int noiseSizeForest;
        public int noiseSizeBeach;
        public int noiseSizeShrubs;
        public int noiseSizeGeoProv;
        public int noiseSizeLandform;

        List<ForceLandform> forceLandforms = new List<ForceLandform>();
        List<ForceClimate> forceClimate = new List<ForceClimate>();

        #endregion

        #region Methods stolen from GenMaps
        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            int pad = TerraGenConfig.geoProvMapPadding;
            mapRegion.GeologicProvinceMap.Data = geologicprovinceGen.GenLayer(
                regionX * noiseSizeGeoProv - pad,
                regionZ * noiseSizeGeoProv - pad,
                noiseSizeGeoProv + 2 * pad,
                noiseSizeGeoProv + 2 * pad
            );
            mapRegion.GeologicProvinceMap.Size = noiseSizeGeoProv + 2 * pad;
            mapRegion.GeologicProvinceMap.TopLeftPadding = mapRegion.GeologicProvinceMap.BottomRightPadding = pad;

            pad = 2;
            mapRegion.ClimateMap.Data = climateGen.GenLayer(
                regionX * noiseSizeClimate - pad,
                regionZ * noiseSizeClimate - pad,
                noiseSizeClimate + 2 * pad,
                noiseSizeClimate + 2 * pad
            );
            mapRegion.ClimateMap.Size = noiseSizeClimate + 2 * pad;
            mapRegion.ClimateMap.TopLeftPadding = mapRegion.ClimateMap.BottomRightPadding = pad;


            mapRegion.ForestMap.Size = noiseSizeForest + 1;
            mapRegion.ForestMap.BottomRightPadding = 1;
            forestGen.SetInputMap(mapRegion.ClimateMap, mapRegion.ForestMap);
            mapRegion.ForestMap.Data = forestGen.GenLayer(regionX * noiseSizeForest, regionZ * noiseSizeForest, noiseSizeForest + 1, noiseSizeForest + 1);

            int upPad = 3;
            mapRegion.UpheavelMap.Size = noiseSizeUpheavel + 2 * upPad;
            mapRegion.UpheavelMap.TopLeftPadding = upPad;
            mapRegion.UpheavelMap.BottomRightPadding = upPad;
            mapRegion.UpheavelMap.Data = upheavelGen.GenLayer(
                regionX * noiseSizeUpheavel - upPad, regionZ * noiseSizeUpheavel - upPad,
                noiseSizeUpheavel + 2 * upPad, noiseSizeUpheavel + 2 * upPad
            );

            int opad = 5;
            mapRegion.OceanMap.Size = noiseSizeOcean + 2 * opad;
            mapRegion.OceanMap.TopLeftPadding = opad;
            mapRegion.OceanMap.BottomRightPadding = opad;
            mapRegion.OceanMap.Data = oceanGen.GenLayer(
                regionX * noiseSizeOcean - opad,
                regionZ * noiseSizeOcean - opad,
                noiseSizeOcean + 2 * opad, noiseSizeOcean + 2 * opad
            );

            mapRegion.BeachMap.Size = noiseSizeBeach + 1;
            mapRegion.BeachMap.BottomRightPadding = 1;
            mapRegion.BeachMap.Data = beachGen.GenLayer(regionX * noiseSizeBeach, regionZ * noiseSizeBeach, noiseSizeBeach + 1, noiseSizeBeach + 1);

            mapRegion.ShrubMap.Size = noiseSizeShrubs + 1;
            mapRegion.ShrubMap.BottomRightPadding = 1;
            bushGen.SetInputMap(mapRegion.ClimateMap, mapRegion.ShrubMap);
            mapRegion.ShrubMap.Data = bushGen.GenLayer(regionX * noiseSizeShrubs, regionZ * noiseSizeShrubs, noiseSizeShrubs + 1, noiseSizeShrubs + 1);

            mapRegion.FlowerMap.Size = noiseSizeForest + 1;
            mapRegion.FlowerMap.BottomRightPadding = 1;
            flowerGen.SetInputMap(mapRegion.ClimateMap, mapRegion.FlowerMap);
            mapRegion.FlowerMap.Data = flowerGen.GenLayer(regionX * noiseSizeForest, regionZ * noiseSizeForest, noiseSizeForest + 1, noiseSizeForest + 1);

            pad = TerraGenConfig.landformMapPadding;
            mapRegion.LandformMap.Data = landformsGen.GenLayer(regionX * noiseSizeLandform - pad, regionZ * noiseSizeLandform - pad, noiseSizeLandform + 2 * pad, noiseSizeLandform + 2 * pad);
            mapRegion.LandformMap.Size = noiseSizeLandform + 2 * pad;
            mapRegion.LandformMap.TopLeftPadding = mapRegion.LandformMap.BottomRightPadding = pad;

            if (chunkGenParams?.HasAttribute("forceLandform") == true)
            {
                var index = chunkGenParams.GetInt("forceLandform");
                for (int i = 0; i < mapRegion.LandformMap.Data.Length; i++)
                {
                    mapRegion.LandformMap.Data[i] = index;
                }
            }

            int regionsize = sapi.WorldManager.RegionSize;
            /*foreach (var fl in forceLandforms)
            {
                forceLandform(mapRegion, regionX, regionZ, pad, regionsize, fl);
                forceNoUpheavel(mapRegion, regionX, regionZ, upPad, regionsize, fl);
            }

            foreach (var climate in forceClimate)
            {
                ForceClimate(mapRegion, regionX, regionZ, pad, regionsize, climate);
            }

            mapRegion.DirtyForSaving = true;
        }



        public static MapLayerBase GetDebugWindMap(long seed)
        {
            MapLayerBase wind = new MapLayerDebugWind(seed + 1);
            wind.DebugDrawBitmap(0, 0, 0, "Wind 1 - Wind");

            return wind;
        }

        public static MapLayerBase GetClimateMapGen(long seed, NoiseClimate climateNoise)
        {
            MapLayerBase climate = new MapLayerClimate(seed + 1, climateNoise);
            climate.DebugDrawBitmap(0, 0, 0, "Climate 1 - Noise");

            climate = new MapLayerPerlinWobble(seed + 2, climate, 6, 0.7f, TerraGenConfig.climateMapWobbleScale, TerraGenConfig.climateMapWobbleScale * 0.15f);
            climate.DebugDrawBitmap(0, 0, 0, "Climate 2 - Perlin Wobble");

            return climate;
        }

        public static MapLayerBase GetOreMap(long seed, NoiseOre oreNoise, float scaleMul, float contrast, float sub)
        {
            MapLayerBase ore = new MapLayerOre(seed + 1, oreNoise, scaleMul, contrast, sub);
            ore.DebugDrawBitmap(DebugDrawMode.RGB, 0, 0, 512, "Ore 1 - Noise");

            ore = new MapLayerPerlinWobble(seed + 2, ore, 5, 0.85f, TerraGenConfig.oreMapWobbleScale, TerraGenConfig.oreMapWobbleScale * 0.15f);
            ore.DebugDrawBitmap(DebugDrawMode.RGB, 0, 0, 512, "Ore 1 - Perlin Wobble");

            return ore;
        }

        public static MapLayerBase GetDepositVerticalDistort(long seed)
        {
            double[] thresholds = new double[] { 0.1, 0.1, 0.1, 0.1 };
            MapLayerPerlin layer = new MapLayerPerlin(seed + 1, 4, 0.8f, 25 * TerraGenConfig.depositVerticalDistortScale, 40, thresholds);

            layer.DebugDrawBitmap(0, 0, 0, "Vertical Distort");

            return layer;
        }

        public static MapLayerBase GetForestMapGen(long seed, int scale)
        {
            MapLayerBase forest = new MapLayerWobbledForest(seed + 1, 3, 0.9f, scale, 600, -100);
            return forest;
        }

        public static MapLayerBase GetGeoUpheavelMapGen(long seed, int scale)
        {
            var map = new MapLayerPerlinUpheavel(seed, upheavelCommonness, scale, 600, -300);
            var blurred = new MapLayerBlur(0, map, 3);
            return blurred;
        }

        public static MapLayerBase GetOceanMapGen(long seed, float landcover, int oceanMapScale, float oceanScaleMul, List<XZ> requireLandAt, bool requiresSpawnOffset)
        {
            var map = new MapLayerOceans(seed, oceanMapScale * oceanScaleMul, landcover, requireLandAt, requiresSpawnOffset);
            var blurred = new MapLayerBlur(0, map, 5);
            return blurred;
        }

        public static MapLayerBase GetBeachMapGen(long seed, int scale)
        {
            MapLayerPerlin layer = new MapLayerPerlin(seed + 1, 6, 0.9f, scale / 3, 255, new double[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f });
            MapLayerBase beach = new MapLayerPerlinWobble(seed + 986876, layer, 4, 0.9f, scale / 2);

            return beach;
        }

        public static MapLayerBase GetGeologicProvinceMapGen(long seed, ICoreServerAPI api)
        {
            MapLayerBase provinces = new MapLayerGeoProvince(seed + 5, api);
            provinces.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, 0, 0, "Geologic Province 1 - WobbleProvinces");

            return provinces;
        }

        public static MapLayerBase GetLandformMapGen(long seed, NoiseClimate climateNoise, ICoreServerAPI api, float landformScale)
        {
            MapLayerBase landforms = new MapLayerLandforms(seed + 12, climateNoise, api, landformScale);
            landforms.DebugDrawBitmap(DebugDrawMode.LandformRGB, 0, 0, "Landforms 1 - Wobble Landforms");

            return landforms;
        }

        #endregion
        */
    }
}
