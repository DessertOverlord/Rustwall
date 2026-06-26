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
using System.Text.RegularExpressions;
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
            World_Params = ringGeneratorWorldParameters;

            ///Here we handle the stuff that GenMaps would normally handle
            var worldConfig = sapi.World.Config;
            LatitudeData latdata = new LatitudeData();
            /*
            int noiseSizeOcean = sapi.WorldManager.RegionSize / TerraGenConfig.oceanMapScale;
            int noiseSizeUpheavel = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeClimate = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeForest = sapi.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            int noiseSizeShrubs = sapi.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            int noiseSizeGeoProv = sapi.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            int noiseSizeLandform = sapi.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            int noiseSizeBeach = sapi.WorldManager.RegionSize / TerraGenConfig.beachMapScale;
            */
/*
            noiseSizeOcean = sapi.WorldManager.RegionSize / TerraGenConfig.oceanMapScale;
            noiseSizeUpheavel = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            noiseSizeClimate = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            noiseSizeForest = sapi.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            noiseSizeShrubs = sapi.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            noiseSizeGeoProv = sapi.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            noiseSizeLandform = sapi.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            noiseSizeBeach = sapi.WorldManager.RegionSize / TerraGenConfig.beachMapScale;
*/
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
/*
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
                );*/
        }

        public string Ring_Name { get; set; }
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
        /*public NewNormalizedSimplexFractalNoise GenTerra_terrainNoise { get; private set; }
        public SimplexNoise GenTerra_distort2dx { get; private set; }
        public SimplexNoise GenTerra_distort2dz { get; private set; }
        public NormalizedSimplexNoise GenTerra_geoUpheavalNoise { get; private set; }*/
/*
        public int GenMaps_noiseSizeOcean { get; private set; }
        public int GenMaps_noiseSizeUpheavel { get; private set; }
        public int GenMaps_noiseSizeClimate { get; private set; }
        public int GenMaps_noiseSizeForest { get; private set; }
        public int GenMaps_noiseSizeShrubs { get; private set; }
        public int GenMaps_noiseSizeGeoProv { get; private set; }
        public int GenMaps_noiseSizeLandform { get; private set; }
        public int GenMaps_noiseSizeBeach { get; private set; }*/
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
            return 1;
        }

        protected override void RustwallStartServerSide()
        {
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
                    //I suspect that this function will count 1 ring short due to how LeftOverRings is computed. This method shaves off the excess
                    //rings before computing the number of total rings in the map, which would leave the rings at the outside edge unaccounted for.
                    //I think instead it should be ((RegionMapSizeX + (ringWidth - LeftOverRings) / ringWidth).
                    // This should theoretically always leave an even division of the RegionMap into rings and account for that extra territory at the edge.
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

            sapi.Event.MapRegionGeneration(HandleRegionLoading, "standard");
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
                    int ringRing = -1;

                    //because regionX or Z cannot have decimal values and the midpoint always contains 0.5 (because there's an even number)
                    //regionX and regionZ can never be equal to the midpoint, therefore only evaling greater and less than is okay.
                    var regionXOffset = regionX - regionMidPoint > 0 ? regionX + safezonediff : regionX - safezonediff;
                    var regionZOffset = regionZ - regionMidPoint > 0 ? regionZ + safezonediff : regionZ - safezonediff;

                    //Region offsets are relative to the center of the map and tell us how far we are from the center point.
                    ringRing = (int)((double.Max(Math.Abs(regionXOffset - regionMidPoint), Math.Abs(regionZOffset - regionMidPoint)) - 0.5) / ringWidth);

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

        private void HandleRegionLoading(IMapRegion region, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            if (NumberOfRings == -500) { return; }
            int ringNum = RingNumberFromRegion(regionX, regionZ);

            region.SetModdata("ringNumber", ringNum);
            //SetWorldParams(RingWorldMaps[ringNum], region, new Vec2i(regionX, regionZ));

            SeedDependentWorldGenParameters worldParams = RingWorldMaps[ringNum];

            
            //Dictionary<string, ByteDataMap2D> newAnimalData = new Dictionary<string, ByteDataMap2D>();

            int[] newBeachData = new int[region.BeachMap.Size * region.BeachMap.Size];
            newBeachData.Fill(255);

            int[] newBiomeData = new int[region.BiomeMap.Size * region.BiomeMap.Size];

            //Dictionary
            //int[] newBlockPatchData = new int[region.BlockPatchMaps.Size ^ 2];

            int[] newClimateData = new int[region.ClimateMap.Size * region.ClimateMap.Size];
            static int PackClimate(int rainfall, int temperature)
            {
                int result = (rainfall & 0xFF) << 8 | ((temperature & 0xFF) << 16);
                return result;
            }
            newClimateData.Fill(PackClimate(0, 0));
            region.ClimateMap.Data = newClimateData;


            int[] newForestData = new int[region.ForestMap.Size * region.ForestMap.Size];
            newForestData.Fill(255);
            region.ForestMap.Data = newForestData;

            int[] newGeoProvData = new int[region.GeologicProvinceMap.Size * region.GeologicProvinceMap.Size];


            int[] newLandformData = new int[region.LandformMap.Size * region.LandformMap.Size];

            string desiredLandform = "realisticflatlands";
            //string desiredLandform = "humongous mountain, cavernless";

            int landformCode = NoiseLandforms.landforms.GetIndexByCode(desiredLandform);
            if (landformCode != -1)
            {
                newLandformData.Fill(landformCode);
                region.LandformMap.Data = newLandformData;
            }

            int[] newOceanData = new int[region.OceanMap.Size * region.OceanMap.Size];
            newOceanData.Fill(0);
            region.OceanMap.Data = newOceanData;

            int[] newOreVerticalDistortBottomData = new int[region.OreMapVerticalDistortBottom.Size * region.OreMapVerticalDistortBottom.Size];

            int[] newOreVerticalDistortTopData = new int[region.OreMapVerticalDistortBottom.Size * region.OreMapVerticalDistortBottom.Size];

            //Dictionary, not int
            //int[] newOreData = new int[region.OreMaps.Size ^ 2];

            //long, not int
            //int[] newRiverData = new int[region.RiverMap.Size ^ 2];

            //Array of arrays, not int
            //int[] newRockStrataData = new int[region.RockStrata.Length ^ 2];

            int[] newShrubData = new int[region.ShrubMap.Size * region.ShrubMap.Size];

            //ushort, not int
            //int[] newTerrainData = new int[region.TerrainMap.Size ^ 2];

            int[] newUpheavelData = new int[region.UpheavelMap.Size * region.UpheavelMap.Size];
            //newUpheavelData.Fill(255);
        }

        //Initialize and load the worldgen parameters
        private void InitRingedWorldGenerator()
        {
            //First, check if this is a brand new world...
            if (sapi.WorldManager.SaveGame.IsNew)
            {
                //If so, we need some new world configuration values.
                //We'll pull in a list of templates from the config
                List<Dictionary<string, double>> presetRingConfigs = config.RingTemplates;
                int TemplatedRings = presetRingConfigs.Count();
                //if the template contains something, then let's look into it
                ///Templates are constructed in a predefined manner:
                ///name
                ///repeat (may or may not be present)
                ///world config values OR a single entry for "random" with a distribution type
                if (TemplatedRings >= 0)
                {
                    foreach (var template in presetRingConfigs)
                    {
                        string name = "";
                        int repeatNum = 0;
                        bool random = false;
                        EnumDistribution randomType = EnumDistribution.NARROWINVERSEGAUSSIAN;
                        int remains = -1;
                        //Dictionary<string, double> ringDict;
                        foreach (KeyValuePair<string, double> kvp in template)
                        {
                            if (kvp.Key.StartsWith("name-"))
                            {
                                name = kvp.Key;
                                template.Remove(kvp.Key);
                                continue;
                            }
                            else if (kvp.Key.Equals("repeat"))
                            {
                                repeatNum = (int)kvp.Value;
                                template.Remove(kvp.Key);
                                continue;
                            }
                            else if (kvp.Key.Equals("random"))
                            {
                                random = true;
                                int randomTypeAsInt = (int)kvp.Value;
                                randomType = (EnumDistribution)randomTypeAsInt;
                                template.Remove(kvp.Key);
                                continue;
                            }
                        }

                        RandomizeParams(out Dictionary<string, double> newParams, out int seed, randomType);

                        foreach (var item in newParams)
                        {
                            if (!template.ContainsKey(item.Key))
                            {
                                template.AddItem(item);
                            }
                        }

                        for (int i = 0; i <= repeatNum; i++)
                        {
                            if (random)
                            {
                                RingWorldMaps.Add(new SeedDependentWorldGenParameters(sapi, seed, newParams));
                            }
                            else
                            {
                                RingWorldMaps.Add(new SeedDependentWorldGenParameters(sapi, seed, template));
                            }
                        }
                    }
                }
                //and if it contains nothing, just go for full random
                else
                {
                    CreateWorldgenValues();
                }
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
            //this stores the generated seeds and params into the savegame, making them persistent
            List<int> seedList = [];
            //yes i couldve used a for loop but foreach is so nice okay?
            foreach (var item in RingWorldMaps)
            {
                seedList.Add(item.World_Seed);
                sapi.WorldManager.SaveGame.StoreData("rustwallRingData_" + RingWorldMaps.IndexOf(item), SerializerUtil.Serialize(item.World_Params));
            }
            sapi.WorldManager.SaveGame.StoreData("rustwallRingSeeds", SerializerUtil.Serialize(seedList));
        }

        private void LoadWorldgenData()
        {
            byte[] seedData = sapi.WorldManager.SaveGame.GetData("rustwallRingSeeds");
            //this could happen if the world is improperly saved after the initial world load.
            //HOPEfully this should never arise.
            if (seedData != null)
            {
                List<int> seedList = SerializerUtil.Deserialize<List<int>>(seedData);
                foreach (var item in seedList)
                {
                    byte[] ringData = sapi.WorldManager.SaveGame.GetData("rustwallRingData_" + seedList.IndexOf(item));
                    if (ringData != null)
                    {
                        Dictionary<string, double> ringDict = SerializerUtil.Deserialize<Dictionary<string, double>>(ringData);
                        RingWorldMaps.Add(new SeedDependentWorldGenParameters(sapi, item, ringDict));
                    }
                    else
                    {
                        sapi.Logger.Error("Failed to load worldgen data for ring " + seedList.IndexOf(item) + ". Ring generator may not work as intended.");
                    }
                }
            }
            else
            {
                CreateWorldgenValues();
            }
        }

        //RandomDoubleInRange does what it says, giving a random double between minVal and maxVal.
        // I can probably eliminate this function entirely at some point but I can't be assed.
        private double RandomDoubleInRange(double minVal, double maxVal)
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
                        newParams.Add(WorldgenParamsToScramble[i], RandomDoubleInRange(WorldgenMinParams[i], WorldgenMaxParams[i]));
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
        private void SetWorldParams(SeedDependentWorldGenParameters worldParams, IMapRegion mapRegion, Vec2i regionCoords)
        {
        }

        private void StopChunkGeneration()
        {
            sapi.WorldManager.AutoGenerateChunks = false;
            sapi.WorldManager.SendChunks = false;
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

            int FromInsideRegionX = (int);
            int ToInsideRegionX = (int);
            int FromOutsideRegionX = (int);
            int ToOutsideRegionX = (int);

            int FromInsideRegionZ = (int);
            int ToInsideRegionZ = (int);
            int FromOutsideRegionZ = (int);
            int ToOutsideRegionZ = (int);

            //This calculation does not account for the new ring / safezone width features
            for (int i = fromRing; i <= toRing; i++)
            {
                //int toRegionX = (int)(i + regionMidPoint + 0.5);
                //var toRegionZ = (int)(i + regionMidPoint + 0.5);
                //var fromRegionX = (int)(regionMidPoint - i - 0.5);
                //var fromRegionZ = (int)(regionMidPoint - i - 0.5);








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
                sapi.WorldManager.TestMapRegionExists(i.X, i.Y, 
                (bool exists) => 
                {
                    if (exists) 
                    {
                        sapi.WorldManager.DeleteMapRegion(i.X, i.Y);
                    }
                    else 
                    {
                        //syntax might be wrong
                        sapi.Logger.Notification("Deletion requested for MapRegion {0}, {1}, but it doesn't exist. Skipping.", i.X, i.Y);
                    }
                });

                for (int j = i.X * chunksInRegion; j < (i.X * chunksInRegion) + chunksInRegion; j++)
                {
                    for (int k = i.Y * chunksInRegion; k < (i.Y * chunksInRegion) + chunksInRegion; k++)
                    {
                        sapi.WorldManager.TestMapChunkExists(j, k, 
                        (bool exists) => 
                        {
                            if (exists) 
                            {
                                sapi.WorldManager.DeleteChunkColumn(j, k);
                            }
                            else 
                            {
                                //syntax might be wrong
                                sapi.Logger.Notification("Deletion requested for MapChunk {0}, {1}, but it doesn't exist. Skipping.", j, k);
                            }
                        });
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
                .BeginSubCommand("info")
                .BeginSubCommand("number")
                    .WithArgs()
                    .HandleWith((args) => 
                    {
                        var callerPos = args.Caller.Pos;
                        var ringNumber = RingNumberFromWorldPos((int)callerPos.X, (int)callerPos.Z);

                        return TextCommandResult.Success("Ring number at region coords is: " + ringNumber);
                    })
                    .EndSubCommand()
                    .BeginSubCommand("params")
                    .WithArgs()
                    .HandleWith((args) =>
                    {
                        var callerPos = args.Caller.Pos;
                        var ringNumber = RingNumberFromWorldPos((int)callerPos.X, (int)callerPos.Z);

                        var output = "";
                        foreach (var item in RingWorldMaps[ringNumber].World_Params)
                        {
                            output += item.Key + " | " + item.Value + "\n";
                        }

                        return TextCommandResult.Success("ring world params for ring " + ringNumber + " are: \n" + output);
                    })
                    .EndSubCommand()
                    .BeginSubCommand("mapdata")
                    .WithArgs()
                    .HandleWith((args) =>
                    {
                        var callerPos = args.Caller.Pos;
                        //var ringNumber = RingNumberFromWorldPos((int)callerPos.X, (int)callerPos.Z);
                        int regionSize = sapi.WorldManager.RegionSize;

                        bool allmax = ((Func<bool>)(() => { 
                            int[] data = sapi.WorldManager.GetMapRegion((int)args.Caller.Pos.X / regionSize, (int)args.Caller.Pos.Z / regionSize).ForestMap.Data.ToArray();
                            IMapRegion mapRegion = sapi.WorldManager.GetMapRegion((int)args.Caller.Pos.X / regionSize, (int)args.Caller.Pos.Z / regionSize);
                            foreach (int item in data)
                            {
                                if (item != 255)
                                {
                                    return false;
                                }
                            }
                            return true;
                        }))();

                        return TextCommandResult.Success("all values in this region's forest map are 255: " + allmax);
                    })
                    .EndSubCommand()
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
        }
    }
}

