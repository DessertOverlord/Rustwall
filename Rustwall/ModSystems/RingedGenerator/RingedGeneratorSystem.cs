using Cairo;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Rustwall.ModSystems.RingedGenerator
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
    public class SeedDependentWorldGenParameters
    {
        public SeedDependentWorldGenParameters(ICoreServerAPI sapi, int seed, Dictionary<string, double> ringGeneratorWorldParameters)
        {
            World_Seed = seed;
            inputParams = ringGeneratorWorldParameters;

            ///Here we handle the stuff that GenMaps would normally handle
            var worldConfig = sapi.World.Config;
            LatitudeData latdata = new LatitudeData();

            int noiseSizeOcean = sapi.WorldManager.RegionSize / TerraGenConfig.oceanMapScale;
            int noiseSizeUpheavel = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeClimate = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeForest = sapi.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            int noiseSizeShrubs = sapi.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            int noiseSizeGeoProv = sapi.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            int noiseSizeLandform = sapi.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            int noiseSizeBeach = sapi.WorldManager.RegionSize / TerraGenConfig.beachMapScale;

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

            //this is a bit ugly and not accurate but GenMaps shits itself without this because
            //requireLandAt is not defined in GenMaps at initialization, but I can't load any later
            //because otherwise HandleRegionLoading is registered too late.
            List<XZ> requireLandAt = new() { new XZ(0, 0) };

            GenMaps_oceanGen = GenMaps.GetOceanMapGen(seed + 1873, landcover, TerraGenConfig.oceanMapScale, oceanscale, requireLandAt, false);
            GenMaps_forestGen = GenMaps.GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            GenMaps_bushGen = GenMaps.GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            GenMaps_flowerGen = GenMaps.GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);
            GenMaps_beachGen = GenMaps.GetBeachMapGen(seed + 2273, TerraGenConfig.beachMapScale);
            GenMaps_geologicprovinceGen = GenMaps.GetGeologicProvinceMapGen(seed + 3, sapi);
            GenMaps_landformsGen = GenMaps.GetLandformMapGen(seed + 4, noiseClimate, sapi, landformScale);

            //Down here, we're moving to the parameters usually handled by GenTerra
            //This is a magic number that is hardcoded in 1.GenTerra. Hopefully it never changes...?
            int terrainGenOctaves = 9;
            float noiseScale;

            noiseScale = Math.Max(1, sapi.WorldManager.MapSizeY / 256f);

            double[] scaleAdjustedFreqs(double[] vs, float horizontalScale)
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] /= horizontalScale;
                }

                return vs;
            }

            GenTerra_terrainNoise = NewNormalizedSimplexFractalNoise.FromDefaultOctaves
                (
                    terrainGenOctaves, 0.0005 * NewSimplexNoiseLayer.OldToNewFrequency / noiseScale, 0.9, seed
                );
            GenTerra_distort2dx = new SimplexNoise
                (
                    [55, 40, 30, 10],
                    scaleAdjustedFreqs([1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65], noiseScale),
                    seed + 9876 + 0
                );
            GenTerra_distort2dz = new SimplexNoise
                (
                    [55, 40, 30, 10],
                    scaleAdjustedFreqs([1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65], noiseScale),
                    seed + 9876 + 2
                );
            GenTerra_geoUpheavalNoise = new NormalizedSimplexNoise
                (
                    [55, 40, 30, 15, 7, 4],
                    scaleAdjustedFreqs([
                        1.0 / 5.5,
                        1.1 / 2.75,
                        1.2 / 1.375,
                        1.2 / 0.715,
                        1.2 / 0.45,
                        1.2 / 0.25
                    ], noiseScale),
                    seed + 9876 + 1
                );
        }

        public Dictionary<string, double> inputParams { get; private set; }
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
        public NewNormalizedSimplexFractalNoise GenTerra_terrainNoise { get; private set; }
        public SimplexNoise GenTerra_distort2dx { get; private set; }
        public SimplexNoise GenTerra_distort2dz { get; private set; }
        public NormalizedSimplexNoise GenTerra_geoUpheavalNoise { get; private set; }
    }

    internal class RingedGeneratorSystem : RustwallModSystem
    {
        //ICoreServerAPI sapi;
        // ringsize must be an even number (? haven't tried an odd number yet) and determines how wide each ring is.
        private int ringWidth;
        private int safeZoneSize;
        public int NumberOfRings { get; private set; }
        // this list is all of the settings we want to mess with. Can be added to easily.
        private readonly List<string> WorldgenParamsToScramble = new List<string> { "landformScale", "globalTemperature", "globalPrecipitation", "globalForestation", "landcover", "oceanscale", "upheavelCommonness", "geologicActivity" };
        // The default parameters for each of the associated parameters to scramble. ORDER MATTERS!
        // Some day I won't have to do this, but I haven't figured out how to gather the currently selected params until
        // after the game is saved for the first time.
        // TODO: programmatically gather the selected worldgen params on first launch.
        private readonly List<double> WorldgenDefaultParams = new List<double> { 1, 1, 1, 0, 0.975, 1, 0.3, 0.05 };
        private static int curRing = 0;
        private static int desiredRing = 0;
        private double regionMidPoint;
        GenMaps mapGenerator;
        GenDeposits depositGenerator;
        public int LeftOverRings { get; private set; }

        public List<SeedDependentWorldGenParameters> RingWorldMaps { get; private set; }


        public override double ExecuteOrder()
        {
            return -0.1;
        }

        protected override void RustwallStartServerSide()
        {
            //if (sapi.WorldManager.SaveGame.IsNew == true) { sapi.Server.ShutDown(); }
            mapGenerator = sapi.ModLoader.GetModSystem<GenMaps>();
            depositGenerator = sapi.ModLoader.GetModSystem<GenDeposits>();

            RegisterChatCommands();

            sapi.Event.ServerRunPhase(EnumServerRunPhase.WorldReady, () => 
            { 
                ringWidth = config.ringWidth;
                safeZoneSize = config.safeZoneSize;
                int RegionMapSizeX = -1;

                // This calculates map size relative to the resolution of the rings
                // It also checks to make sure the world is a square; if it is rectangular, the ring generator doesn't initialize
                if (sapi.WorldManager.MapSizeX == sapi.WorldManager.MapSizeZ)
                {
                    RegionMapSizeX = (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) / 2;
                    LeftOverRings = RegionMapSizeX % ringWidth;
                    NumberOfRings = LeftOverRings == 0 ? (RegionMapSizeX / ringWidth) : ((RegionMapSizeX - LeftOverRings) / ringWidth);
                }
                else 
                {
                    NumberOfRings = -500;
                }

                regionMidPoint = ((RegionMapSizeX + RegionMapSizeX - 1) / 2.0);
                RingWorldMaps = new List<SeedDependentWorldGenParameters>(NumberOfRings);
            });

            sapi.Event.InitWorldGenerator(() => InitRingedWorldGenerator(), "standard");

            //Add the region method to the MapRegionGeneration event, causing it to be called any time the engine wants to generate a new region
            sapi.Event.MapRegionGeneration(HandleRegionLoading, "standard");

            //Add the chunk method to MapChunkGeneration; this is triggered any time a new chunk column is requested.
            sapi.Event.MapChunkGeneration(HandleChunkLoading, "standard");
        }

        public int RingNumberFromRegion(int regionX, int regionZ)
        {
            if (safeZoneSize != ringWidth)
            {
                int safezonediff = Math.Abs(ringWidth - safeZoneSize);

                int safeZoneRing = (int)(((double.Max(Math.Abs(regionX - regionMidPoint), Math.Abs(regionZ - regionMidPoint)) - 0.5)) / safeZoneSize);

                if (safeZoneRing == 0)
                {
                    return safeZoneRing;
                }
                else
                {
                    //TODO: this could be made cleaner by calculating the regionX and regionZ offsets separately
                    //DONE: Looks much better!
                    int ringRing = -1;

                    //because regionX or Z cannot have decimal values and the midpoint always contains 0.5 (because there's an even number)
                    //regionX and regionZ can never be equal, therefore only evaling greater and less than is okay.
                    var regionXOffset = regionX - regionMidPoint > 0 ? regionX + safezonediff : regionX - safezonediff;
                    var regionZOffset = regionZ - regionMidPoint > 0 ? regionZ + safezonediff : regionZ - safezonediff;

                    ringRing = (int)((double.Max(Math.Abs(regionXOffset - regionMidPoint), Math.Abs(regionZOffset - regionMidPoint)) - 0.5) / ringWidth);

                    /*
                    if (regionX - regionMidPoint > 0 && regionZ - regionMidPoint > 0)
                    {
                        ringRing = (int)((double.Max(Math.Abs(regionX + safezonediff - regionMidPoint), Math.Abs(regionZ + safezonediff - regionMidPoint)) - 0.5) / ringWidth);
                    }
                    else if (regionX - regionMidPoint > 0 && regionZ - regionMidPoint < 0)
                    {
                        ringRing = (int)((double.Max(Math.Abs(regionX + safezonediff - regionMidPoint), Math.Abs(regionZ - safezonediff - regionMidPoint)) - 0.5) / ringWidth);
                    }
                    else if (regionX - regionMidPoint < 0 && regionZ - regionMidPoint < 0)
                    {
                        ringRing = (int)((double.Max(Math.Abs(regionX - safezonediff - regionMidPoint), Math.Abs(regionZ - safezonediff - regionMidPoint)) - 0.5) / ringWidth);
                    }
                    else if (regionX - regionMidPoint < 0 && regionZ - regionMidPoint > 0)
                    {
                        ringRing = (int)((double.Max(Math.Abs(regionX - safezonediff - regionMidPoint), Math.Abs(regionZ + safezonediff - regionMidPoint)) - 0.5) / ringWidth);
                    }*/

                    return ringRing;
                }
            }
            else
            {
                return (int)(((double.Max(Math.Abs(regionX - regionMidPoint), Math.Abs(regionZ - regionMidPoint)) - 0.5)) / ringWidth);
            }
        }

        public int RingNumberFromChunk(int chunkX, int chunkZ) 
        {
            int regionX = chunkX / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
            int regionZ = chunkZ / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
            return RingNumberFromRegion(regionX, regionZ);
        }
        
        public int RingNumberFromWorldPos(int posX, int posZ)
        {
            int regionX = posX / sapi.WorldManager.RegionSize;
            int regionZ = posZ / sapi.WorldManager.RegionSize;
            return RingNumberFromRegion(regionX, regionZ);
        }

        private void HandleChunkLoading(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            if (NumberOfRings == -500) { return; }

            int regionX = chunkX / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
            int regionZ = chunkZ / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);

            //HandleRegionLoading(null, regionX, regionZ);
            
        }

        private void HandleRegionLoading(IMapRegion region, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            if (NumberOfRings == -500) { return; }
            desiredRing = RingNumberFromRegion(regionX, regionZ);
            if (curRing != desiredRing)
            {
                Debug.WriteLine("Changing ring generator. Old ring was " + curRing + ", new ring is " + desiredRing);

                curRing = desiredRing;

                //var tempvar = ringDictList[curRing];
                //var tempvar2 = seedList[curRing];

                //region.SetModdata("pos", new Vec2i(regionX, regionZ));

                SetWorldParams(RingWorldMaps[curRing]);

                //Deprecated -- prefer using RingWorldMaps
                //SetWorldParams(region, ringDictList[curRing], seedList[curRing]);
            }
        }

        //Initialize and load the worldgen parameters
        private void InitRingedWorldGenerator()
        {
            //pull in list of stuff from config
            List<Dictionary<string, double>> presetRingConfigs = config.RingTemplates;

            int TemplatedRings = presetRingConfigs.Count();

            //if the template is empty, everything is random
            if (TemplatedRings == 0) 
            {
                //If this is the first world load, we need some fresh params

            }

            if (sapi.WorldManager.SaveGame.IsNew)
            {
                CreateWorldgenValues();
            }
            // if it isn't, just load what's already there (hopefully...)
            else
            {
                LoadWorldgenData();
            }
        }

        //Initialize first-time world generator values
        private void CreateWorldgenValues()
        {
            //Initialize seedList and ringDictList, and loop through them to populate values
            //seedList.Add(sapi.WorldManager.SaveGame.Seed);
            List<int> seedList = new List<int>(NumberOfRings) { sapi.WorldManager.SaveGame.Seed };
            List<Dictionary<string, double>> ringDictList = new List<Dictionary<string, double>>(NumberOfRings) { new Dictionary<string, double>() };
            //This adds the default worldgen params to spawn (ring 0)
            for (int i = 0; i < WorldgenParamsToScramble.Count(); i++)
            {
                ringDictList[0].Add(WorldgenParamsToScramble[i], WorldgenDefaultParams[i]);
            }

            // it needs to be less than or equal to because I want exactly 26 (minus the first one already added), not 25. Otherwise shit goes sideways!
            for (int i = 1; i <= NumberOfRings; i++)
            {
                RandomizeParams(out Dictionary<string, double> newParams, out int seed, EnumDistribution.NARROWINVERSEGAUSSIAN);
                seedList.Add(seed);
                ringDictList.Add(newParams);
            }

            for (int i = 0; i <= NumberOfRings; i++)
            {
                RingWorldMaps.Add(new SeedDependentWorldGenParameters(sapi, seedList[i], ringDictList[i]));
            }

            StoreWorldgenData();
        }

        private void StoreWorldgenData()
        {
            // this stores the generated seeds and params into the savegame, making them persistent
            sapi.WorldManager.SaveGame.StoreData("rustwallRingMaps", SerializerUtil.Serialize(RingWorldMaps));
        }

        private void LoadWorldgenData()
        {
            byte[] mapData = sapi.WorldManager.SaveGame.GetData("rustwallRingMaps");
            //this could happen if the world is improperly saved after the initial world load.
            //HOPEfully this should never arise.
            if (mapData != null)
            {
                RingWorldMaps = SerializerUtil.Deserialize<List<SeedDependentWorldGenParameters>>(mapData);
            }
            else
            {
                CreateWorldgenValues();
            }
        }

        //RandomDoubleInRange does what it says, giving a random double between minVal and maxVal.
        // I can probably eliminate this function entirely at some point but I can't be assed.
        private double RandomDoubleInRange(ICoreServerAPI api, double minVal, double maxVal)
        {
            return sapi.World.Rand.NextDouble() * (maxVal - minVal) + minVal;
        }

        //RandomizeParams takes WorldgenParamsToScramble and loops through every world generator parameter, creating a dictionary
        // of randomized values by calling RandomDoubleInRange. The dictionary is passed out via "out" params. The seed is also randomized.
        private void RandomizeParams(out Dictionary<string, double> newParams, out int newSeed, EnumDistribution dist = EnumDistribution.NARROWINVERSEGAUSSIAN)
        {
            newParams = new Dictionary<string, double>();
            // These are the hardcoded min and max values for the attributes we want to scramble.
            // TODO: Can I get these programmatically, instead of hardcoding them?
            var WorldgenMinParams = new List<double> { 0.5, 0, 0, -1, 0.1, 0.1, 0, 0 };
            var WorldgenMaxParams = new List<double> { 1.5, 5, 5, 1, 1, 4, 1, 0.4 };
            var WorldgenAverageParams = new List<double> { 1, 2.5, 2.5, 0, 0.55, 2.05, 0.5, 0.2 };
            var WorldgenVarianceParams = new List<double> { .5, 2.5, 2.5, 1, 0.45, 1.95, 0.5, 0.2 };

            
            switch (dist)
            {
                case EnumDistribution.UNIFORM:
                    for (int i = 0; i < WorldgenParamsToScramble.Count; i++)
                    {
                        newParams.Add(WorldgenParamsToScramble[i], RandomDoubleInRange(sapi, WorldgenMinParams[i], WorldgenMaxParams[i]));
                    }
                    break;
                case EnumDistribution.NARROWINVERSEGAUSSIAN:
                    for (int i = 0; i < WorldgenParamsToScramble.Count; i++)
                    {
                        var natfl = NatFloat.create(dist, (float)WorldgenAverageParams[i], (float)WorldgenVarianceParams[i]);
                        newParams.Add(WorldgenParamsToScramble[i], natfl.nextFloat());
                    }
                    break;
                default:
                    break;
            }
            newSeed = sapi.World.Seed + sapi.World.Rand.Next(1000);
        }

        private void RandomizeRing(int ringNumber, EnumDistribution dist = EnumDistribution.NARROWINVERSEGAUSSIAN)
        {
            RandomizeParams(out Dictionary<string, double> newParams, out int newSeed, dist);

            RingWorldMaps[ringNumber] = new SeedDependentWorldGenParameters(sapi, newSeed, newParams);

            /*
            ringDictList[ringNumber].Clear();
            ringDictList[ringNumber].AddRange(newParams);

            seedList[ringNumber] = newSeed;*/

        }

        private void RandomizeRingRange(int fromRing, int toRing, EnumDistribution dist = EnumDistribution.NARROWINVERSEGAUSSIAN)
        {
            for (int i = fromRing; i <= toRing; i++)
            {
                RandomizeRing(i, dist);
            }
        }

        //SetWorldParams takes the parameters and seed provided and updates the world generator with them.
        [Obsolete]
        private void SetWorldParams(IMapRegion mapRegion, Dictionary<string, double> ringGenKVP, int seed)
        {
            //StopChunkGeneration();

            //sapi.WorldManager.SaveGame.Seed = seed;
            
            
            var worldConfig = sapi.World.Config;

            LatitudeData latdata = new LatitudeData();

            /// We're directly modifying the values that GenMaps.cs uses to instruct the world generator.
            /// We do NOT need to change any noiseSize vars because they are computed off of a hardcoded value and a region size value, 
            /// neither of which we are changing.

            int noiseSizeOcean = sapi.WorldManager.RegionSize / TerraGenConfig.oceanMapScale;
            int noiseSizeUpheavel = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeClimate = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeForest = sapi.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            int noiseSizeShrubs = sapi.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            int noiseSizeGeoProv = sapi.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            int noiseSizeLandform = sapi.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            int noiseSizeBeach = sapi.WorldManager.RegionSize / TerraGenConfig.beachMapScale;

            float tempModifier = (float)ringGenKVP["globalTemperature"];
            float rainModifier = (float)ringGenKVP["globalPrecipitation"];
            //latdata.polarEquatorDistance = worldConfig.GetString("polarEquatorDistance", "50000").ToInt(50000);
            float upheavelCommonness = (float)ringGenKVP["upheavelCommonness"];
            float landcover = (float)ringGenKVP["landcover"];
            float oceanscale = (float)ringGenKVP["oceanscale"];
            float landformScale = (float)ringGenKVP["landformScale"];

            //DO NOT CHANGE! Polar-eq dist is not a worldconfig the rings change!
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

                    noiseClimate = new NoiseClimateRealistic(seed, (double)sapi.WorldManager.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale, latdata.polarEquatorDistance, spawnMinTemp, spawnMaxTemp);
                    (noiseClimate as NoiseClimateRealistic).GeologicActivityStrength = (float)ringGenKVP["geologicActivity"];

                    latdata.isRealisticClimate = true;
                    latdata.ZOffset = (noiseClimate as NoiseClimateRealistic).ZOffset;
                    break;
                    
                default:
                    noiseClimate = new NoiseClimatePatchy(seed);
                    break;
            }

            noiseClimate.rainMul = rainModifier;
            noiseClimate.tempMul = tempModifier;
            
            mapGenerator.climateGen = GenMaps.GetClimateMapGen(seed + 1, noiseClimate);
            mapGenerator.upheavelGen = GenMaps.GetGeoUpheavelMapGen(seed + 873, TerraGenConfig.geoUpheavelMapScale);
            mapGenerator.oceanGen = GenMaps.GetOceanMapGen(seed + 1873, landcover, TerraGenConfig.oceanMapScale, oceanscale, mapGenerator.requireLandAt, false);
            mapGenerator.forestGen = GenMaps.GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            mapGenerator.bushGen = GenMaps.GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            mapGenerator.flowerGen= GenMaps.GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);
            mapGenerator.beachGen = GenMaps.GetBeachMapGen(seed + 2273, TerraGenConfig.beachMapScale);
            mapGenerator.geologicprovinceGen = GenMaps.GetGeologicProvinceMapGen(seed + 3, sapi);
            mapGenerator.landformsGen = GenMaps.GetLandformMapGen(seed + 4, noiseClimate, sapi, landformScale);
        }

        private void SetWorldParams(SeedDependentWorldGenParameters worldParams)
        {
            mapGenerator.climateGen = worldParams.GenMaps_climateGen;
            mapGenerator.upheavelGen = worldParams.GenMaps_upheavelGen;
            mapGenerator.oceanGen = worldParams.GenMaps_oceanGen;
            mapGenerator.forestGen = worldParams.GenMaps_forestGen;
            mapGenerator.bushGen = worldParams.GenMaps_bushGen;
            mapGenerator.flowerGen = worldParams.GenMaps_flowerGen;
            mapGenerator.beachGen = worldParams.GenMaps_beachGen;
            mapGenerator.geologicprovinceGen = worldParams.GenMaps_geologicprovinceGen;
            mapGenerator.landformsGen = worldParams.GenMaps_landformsGen;
        }


        //Turns off chunk generation and sending to clients, reloads all of the worldgen parameters (seed, multipliers), and then re-enables everything.
        // Necessary any time we change what ring we're generating.
        // Deprecated
        /*private void RestartChunkGenerator()
        {
            // This was built using the logic for /wgen regen as a template. The command paused the chunkdbthread before doing any of this type of stuff,
            // but this mod seems to function fine without doing that... hopefully it wasn't important :^)

            StopChunkGeneration();
            
            //sapi.WorldManager.AutoGenerateChunks = false;
            //sapi.WorldManager.SendChunks = false;

            ReloadWorldgenAssets();
            //sapi.Assets.Reload(new AssetLocation("worldgen/"));
            //var patchLoader = sapi.ModLoader.GetModSystem<ModJsonPatchLoader>();
            //patchLoader.ApplyPatches("worldgen/");
            //sapi.Event.TriggerInitWorldGen();

            StartChunkGeneration();
            //sapi.WorldManager.AutoGenerateChunks = true;
            //sapi.WorldManager.SendChunks = true;
        }*/

        private void StopChunkGeneration()
        {
            sapi.WorldManager.AutoGenerateChunks = false;
            sapi.WorldManager.SendChunks = false;
        }

        /// <summary>
        /// Deprecated
        /// </summary>
        [Obsolete]
        private void ReloadWorldgenAssets()
        {
            //sapi.Assets.Reload(new AssetLocation("worldgen/"));
            //var patchLoader = sapi.ModLoader.GetModSystem<ModJsonPatchLoader>();
            //patchLoader.ApplyPatches("worldgen/");
            sapi.Event.TriggerInitWorldGen();
        }

        private void StartChunkGeneration()
        {
            sapi.WorldManager.AutoGenerateChunks = true;
            sapi.WorldManager.SendChunks = true;

            int chSize = sapi.WorldManager.ChunkSize;
            var allPlayers = sapi.World.AllOnlinePlayers;

            foreach (var ply in allPlayers)
            {
                var sply = (ply as IServerPlayer);
                var eply = ply.Entity;
                sply.CurrentChunkSentRadius = 0;
                //sapi.WorldManager.ForceSendChunkColumn(sply, (int)(eply.Pos.X / chSize), (int)(eply.Pos.Z / chSize), 1);
                //Debug.WriteLine("Sent chunk " + (int)(eply.Pos.X / chSize) + ", " + (int)(eply.Pos.Z / chSize) + " to player " + sply.PlayerName);
                eply.TeleportToDouble(eply.Pos.X, eply.Pos.Y, eply.Pos.Z);
            }
        }
        //Given a range of rings, erase them and mangle the worldgen params
        private void DeleteRingRange(int fromRing, int toRing) 
        {
            List<Vec2i> regionCoordsToDelete = new List<Vec2i>();
            int chSize = sapi.WorldManager.ChunkSize;
            int chunksInRegion = (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);


            for (int i = fromRing; i <= toRing; i++)
            {
                int toRegionX = (int)(i + regionMidPoint + 0.5);
                var toRegionZ = (int)(i + regionMidPoint + 0.5);
                var fromRegionX = (int)(regionMidPoint - i - 0.5);
                var fromRegionZ = (int)(regionMidPoint - i - 0.5);
                for (int j = fromRegionX; j <= toRegionX; j++)
                {
                    regionCoordsToDelete.Add(new Vec2i(j, fromRegionZ));
                    regionCoordsToDelete.Add(new Vec2i(j, toRegionZ));
                }
                for (int j = fromRegionZ; j <= toRegionZ; j++)
                {
                    regionCoordsToDelete.Add(new Vec2i(fromRegionX, j));
                    regionCoordsToDelete.Add(new Vec2i(toRegionX, j));
                }
            }

            foreach (var i in regionCoordsToDelete)
            {
                sapi.WorldManager.DeleteMapRegion(i.X, i.Y);

                for (int j = i.X * chunksInRegion; j < (i.X * chunksInRegion) + chunksInRegion; j++)
                {
                    for (int k = i.Y * chunksInRegion; k < (i.Y * chunksInRegion) + chunksInRegion; k++)
                    {
                        sapi.WorldManager.DeleteChunkColumn(j, k);
                    }
                }
            }
        }

        public void TriggerGreatDecay(int fromRing, int toRing)
        {
            //We are not allowed to regen ring 0 (the innermost safe zone). This hardcodes that in even if players let the stability get to 0
            if (fromRing <= 0)
            {
                Debug.WriteLine("Rustwall error: fromRing was less than or equal to 0. Safezone deletions are forbidden. Changing to 1.");
                fromRing = 1;
            }

            if (toRing <= 0)
            {
                Debug.WriteLine("Rustwall error: toRing was less than or equal to 0. Safezone deletions are forbidden. Changing to 1.");
                toRing = 1;
            }

            if (toRing > NumberOfRings)
            {
                Debug.WriteLine("Rustwall error: requested deletion exceeds size of ring map. Try a smaller value.");
                return;
            }

            if (fromRing > toRing)
            {
                Debug.WriteLine("Rustwall error: fromRing was greater than toRing. What the fuck did you do?");
                return;
            }

            if (fromRing >= NumberOfRings)
            {
                fromRing = NumberOfRings - 1;
                Debug.WriteLine("Rustwall error: fromRing exceeded size of ring map. This will crash the fuck out of the server");
            }

            if (toRing >= NumberOfRings)
            {
                toRing = NumberOfRings - 1;
                Debug.WriteLine("Rustwall error: toRing exceeded size of ring map. This will crash the fuck out of the server");
            }

            StopChunkGeneration();
            RandomizeRingRange(fromRing, toRing, EnumDistribution.UNIFORM);
            StoreWorldgenData();
            DeleteRingRange(fromRing, toRing);
            StartChunkGeneration();
        }

        public void TriggerGreatDecay(float stabRatio)
        {
            int fromRing = (int)(NumberOfRings - (NumberOfRings * stabRatio));
            int toRing = NumberOfRings;



            TriggerGreatDecay(fromRing, toRing);
        }

        public void TriggerGreatDecay(int ring)
        {
            TriggerGreatDecay(ring, ring);
        }

        private void RegisterChatCommands()
        {
            sapi.ChatCommands.Create("rustwall")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithDescription("Manage rustwall-specific functions")
                .BeginSubCommand("whatringhere")
                .WithArgs()
                .HandleWith((args) => 
                {
                    var callerPos = args.Caller.Pos;
                    var ringNumber = RingNumberFromWorldPos((int)callerPos.X, (int)callerPos.Z);


                    return TextCommandResult.Success("Ring number at region coords is: " + ringNumber);
                })
                .EndSubCommand()
                .BeginSubCommand("delete")

                    .BeginSubCommand("ring")
                    .WithArgs(sapi.ChatCommands.Parsers.Int("ring"))
                    .HandleWith((args) =>
                    {
                        TriggerGreatDecay((int)args[0]);

                        return TextCommandResult.Success("Deleted ring " + (int)args[0]);
                    })
                    .EndSubCommand()

                    .BeginSubCommand("ringrange")
                    .WithArgs(sapi.ChatCommands.Parsers.Int("fromRing"), sapi.ChatCommands.Parsers.Int("toRing"))
                    .HandleWith((args) =>
                    {
                        TriggerGreatDecay((int)args[0], (int)args[1]);

                        return TextCommandResult.Success("deleted some shit prolly");
                    })
                    .EndSubCommand()

                    .BeginSubCommand("ratio")
                    .WithArgs(sapi.ChatCommands.Parsers.Float("ratio"))
                    .HandleWith((args) =>
                    {
                        TriggerGreatDecay((float)args[0]);

                        return TextCommandResult.Success("deleted some shit prolly");
                    })
                    .EndSubCommand()

                .EndSubCommand();

            /*sapi.ChatCommands.Create("PrintWorldConfig")
                .WithDescription("Does what it says on the tin.")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs()
                .HandleWith((args) =>
                {

                    var worldconfig = sapi.WorldManager.SaveGame.WorldConfiguration;
                    //var worldconfig = sapi.World.Config;
                    string result = "";
                    foreach (var item in WorldgenParamsToScramble)
                    {
                        result += worldconfig.GetDouble(item) != null ? item + ": " + worldconfig.GetDouble(item) + "\n" : item + "null value\n";
                    }

                    return TextCommandResult.Success("Values: " + result);
                });*/
        }

        //[HarmonyPatchCategory("RingedGeneratorSystem")]
        [HarmonyPatch(typeof(GenDeposits), nameof(GenDeposits.GeneratePartial))]
        class GenDeposits_GeneratePartial_Patch
        {
            static void Prefix(IServerChunk[] chunks, int chunkX, int chunkZ, int chunkdX, int chunkdZ, ref float ___chanceMultiplier, ICoreAPI ___api)
            {
                RingedGeneratorSystem ringModSys = ___api.ModLoader.GetModSystem<RingedGeneratorSystem>();
                int ringNumber = ringModSys.RingNumberFromChunk(chunkX, chunkZ);
                //testing
                if (ringNumber > 1) 
                {
                    ___chanceMultiplier = 0;//someListOfValuesOrMathHere[ringNumber];
                }
                else 
                {
                    ___chanceMultiplier = 3;//someListOfValuesOrMathHere[ringNumber];
                }
            }
        }

        [HarmonyPatch]
        class GenTerra_OnChunkColumnGen_Patch
        {
            public static MethodInfo TargetMethod()
            {
                Type[] methodParams =
                [
                    typeof(IChunkColumnGenerateRequest)
                ];

                var output = AccessTools.Method(typeof(GenTerra), "OnChunkColumnGen");

                return output;
            }

            public static void Postfix(
                IChunkColumnGenerateRequest request,
                int ___terrainGenOctaves,
                ICoreServerAPI ___api,
                float ___noiseScale,
                NewNormalizedSimplexFractalNoise ___terrainNoise,
                SimplexNoise ___distort2dx,
                SimplexNoise ___distort2dz,
                NormalizedSimplexNoise ___geoUpheavalNoise
                )
            {
                var ringsys = ___api.ModLoader.GetModSystem<RingedGeneratorSystem>();
                int ringNum = ringsys.RingNumberFromChunk(request.ChunkX, request.ChunkZ);
                //int seed = ringsys.seedList[ringNum];
                ___terrainNoise = ringsys.RingWorldMaps[ringNum].GenTerra_terrainNoise;
                ___distort2dx = ringsys.RingWorldMaps[ringNum].GenTerra_distort2dx;
                ___distort2dz = ringsys.RingWorldMaps[ringNum].GenTerra_distort2dx;
                ___geoUpheavalNoise = ringsys.RingWorldMaps[ringNum].GenTerra_geoUpheavalNoise;
            }
        //Deprecated patch
        /*
        [HarmonyPatch]
        class GenTerra_OnChunkColumnGen_Patch
        {
            public static MethodInfo TargetMethod()
            {
                Type[] methodParams =
                [
                    typeof(IChunkColumnGenerateRequest)
                ];

                var output = AccessTools.Method(typeof(GenTerra), "OnChunkColumnGen");

                return output;
            }

            public static void Postfix(
                IChunkColumnGenerateRequest request, 
                int ___terrainGenOctaves,
                ICoreServerAPI ___api,
                float ___noiseScale,
                NewNormalizedSimplexFractalNoise ___terrainNoise,
                SimplexNoise ___distort2dx,
                SimplexNoise ___distort2dz,
                NormalizedSimplexNoise ___geoUpheavalNoise
                )
            {
                var ringsys = ___api.ModLoader.GetModSystem<RingedGeneratorSystem>();
                int ringNum = ringsys.RingNumberFromChunk(request.ChunkX, request.ChunkZ);
                int seed = ringsys.seedList[ringNum];
                //___api.WorldManager.SaveGame.Seed = ringsys.seedList[ringNum];

                double[] scaleAdjustedFreqs(double[] vs, float horizontalScale)
                {
                    for (int i = 0; i < vs.Length; i++)
                    {
                        vs[i] /= horizontalScale;
                    }

                    return vs;
                }

                ___terrainNoise = NewNormalizedSimplexFractalNoise.FromDefaultOctaves
                    (
                        ___terrainGenOctaves, 0.0005 * NewSimplexNoiseLayer.OldToNewFrequency / ___noiseScale, 0.9, seed
                    );
                ___distort2dx = new SimplexNoise
                    (
                        new double[] { 55, 40, 30, 10 },
                        scaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, ___noiseScale),
                        seed + 9876 + 0
                    );
                ___distort2dz = new SimplexNoise
                    (
                        new double[] { 55, 40, 30, 10 },
                        scaleAdjustedFreqs(new double[] { 1 / 5.0, 1 / 2.50, 1 / 1.250, 1 / 0.65 }, ___noiseScale),
                        seed + 9876 + 2
                    );
                ___geoUpheavalNoise = new NormalizedSimplexNoise
                    (
                        new double[] { 55, 40, 30, 15, 7, 4 },
                        scaleAdjustedFreqs(new double[] {
                        1.0 / 5.5,
                        1.1 / 2.75,
                        1.2 / 1.375,
                        1.2 / 0.715,
                        1.2 / 0.45,
                        1.2 / 0.25
                        }, ___noiseScale),
                        seed + 9876 + 1
                    );
            }*/
        }
    }
}

