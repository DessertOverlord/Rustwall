using ProtoBuf;
using Rustwall.Configs;
using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

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

        public Dictionary<int, RGWorldgenTemplate> RingTemplates = new Dictionary<int, RGWorldgenTemplate>();
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
                    int RegionMapSizeXWithoutSafeZone = RegionMapSizeX - safeZoneSize;
                    LeftOverRings = RegionMapSizeX % ringWidth;
                    //I suspect that this function will count 1 ring short due to how LeftOverRings is computed. This method shaves off the excess
                    //rings before computing the number of total rings in the map, which would leave the rings at the outside edge unaccounted for.
                    //I think instead it should be ((RegionMapSizeX + (ringWidth - LeftOverRings) / ringWidth).
                    // This should theoretically always leave an even division of the RegionMap into rings and account for that extra territory at the edge.
                    NumberOfRings = LeftOverRings == 0 ? (RegionMapSizeX / ringWidth) + 1 : ((RegionMapSizeX - LeftOverRings) / ringWidth) + 2;
                }
                else 
                {
                    NumberOfRings = -500;
                }

                regionMidPoint = ((RegionMapSizeX + RegionMapSizeX - 1) / 2.0);
                RingWorldMaps = new List<SeedDependentWorldGenParameters>(NumberOfRings);
                //RingTemplates = new List<RGWorldgenTemplate>(NumberOfRings);
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

            /// Can happen if someone skips a ring in the template list.
            if (!RingTemplates.TryGetValue(ringNum, out RGWorldgenTemplate ParamsToUse))
            {
                return;
            }

            if (ParamsToUse.beachData > -1)
            {
                int[] newBeachData = new int[region.BeachMap.Size * region.BeachMap.Size];
                newBeachData.Fill(ParamsToUse.beachData);
                region.BeachMap.Data = newBeachData;
            }

            /// Not sure what BiomeData represents
            //int[] newBiomeData = new int[region.BiomeMap.Size * region.BiomeMap.Size];

            /// Blockpatches seem to mostly govern things like shrubs and mushrooms. Not sure 
            /// I care that much about this (could I make LSD land...?)
            //Dictionary
            //int[] newBlockPatchData = new int[region.BlockPatchMaps.Size ^ 2];


            if (ParamsToUse.rainfallData > -1 && ParamsToUse.temperatureData > -1)
            {

                int[] newClimateData = new int[region.ClimateMap.Size * region.ClimateMap.Size];
                static int PackClimate(int rainfall, int temperature)
                {
                    int result = (rainfall & 0xFF) << 8 | ((temperature & 0xFF) << 16);
                    return result;
                }
                newClimateData.Fill(PackClimate(ParamsToUse.rainfallData, ParamsToUse.temperatureData));
                region.ClimateMap.Data = newClimateData;

                /// I need a way to handle the case where only one climate parameter is set.
                /// I'll do it by taking the existing value from the map, unpacking it, and repacking it 
                /// with the other parameter. For now I will assume that I am always specifying both.

                /*
                if (ParamsToUse.rainfallData <= -1)
                {
                    int[] newClimateData = new int[region.ClimateMap.Size * region.ClimateMap.Size];
                    static int PackClimate(int rainfall, int temperature)
                    {
                        int result = (rainfall & 0xFF) << 8 | ((temperature & 0xFF) << 16);
                        return result;
                    }
                    newClimateData.Fill(PackClimate(ParamsToUse.rainfallData, ParamsToUse.temperatureData));
                    region.ClimateMap.Data = newClimateData;
                }
                if (ParamsToUse.temperatureData > -1)
                {
                    int[] newClimateData = new int[region.ClimateMap.Size * region.ClimateMap.Size];
                    static int PackClimate(int rainfall, int temperature)
                    {
                        int result = (rainfall & 0xFF) << 8 | ((temperature & 0xFF) << 16);
                        return result;
                    }
                    newClimateData.Fill(PackClimate(ParamsToUse.rainfallData, ParamsToUse.temperatureData));
                    region.ClimateMap.Data = newClimateData;
                }*/
            }

            if (ParamsToUse.forestData > -1)
            {
                int[] newForestData = new int[region.ForestMap.Size * region.ForestMap.Size];
                newForestData.Fill(ParamsToUse.forestData);
                region.ForestMap.Data = newForestData;
            }

            /// Not yet implemented and I don't know what it does
            //int[] newGeoProvData = new int[region.GeologicProvinceMap.Size * region.GeologicProvinceMap.Size];

            int[] newLandformData = new int[region.LandformMap.Size * region.LandformMap.Size];
            string desiredLandform = ParamsToUse.landformData;
            if (desiredLandform != null || desiredLandform != "")
            {
                int landformCode = NoiseLandforms.landforms.GetIndexByCode(desiredLandform);
                if (landformCode != -1)
                {
                    newLandformData.Fill(landformCode);
                    region.LandformMap.Data = newLandformData;
                }
                else
                {
                    sapi.Logger.Error("Failed to find landform code for " + desiredLandform + ". Landform map will be unaltered.");
                }
            }

            /// -1 is the default value, which means "don't change it"
            if (ParamsToUse.oceanData > -1)
            {
                int[] newOceanData = new int[region.OceanMap.Size * region.OceanMap.Size];
                newOceanData.Fill(ParamsToUse.oceanData);
                region.OceanMap.Data = newOceanData;
            }

            /// Not used right now. Not really sure how much I care about putting these in.
            //int[] newOreVerticalDistortBottomData = new int[region.OreMapVerticalDistortBottom.Size * region.OreMapVerticalDistortBottom.Size];
            //int[] newOreVerticalDistortTopData = new int[region.OreMapVerticalDistortBottom.Size * region.OreMapVerticalDistortBottom.Size];
            
            if (ParamsToUse.oreData != null)
            {
                static int PackOreValues(OreValues values)
                {
                    return (values.value & 0xFF) | ((values.hypercommonness & 0xFF) << 8) | ((values.richness & 0xFF) << 16);
                }

                foreach (var kvp in ParamsToUse.oreData)
                {
                    if (region.OreMaps.TryGetValue(kvp.Key, out IntDataMap2D oreData))
                    {
                        int[] newOreData = new int[oreData.Size * oreData.Size];
                        newOreData.Fill(PackOreValues(kvp.Value));
                        oreData.Data = newOreData;
                    }
                    else
                    {
                        sapi.Logger.Error($"Failed to find ore map for {kvp.Key}. Ore map for {kvp.Key} will be unaltered.");
                    }
                }
            }

            /// Not implemented. Probably doesn't really do anything right now?
            //long, not int
            //int[] newRiverData = new int[region.RiverMap.Size ^ 2];

            /// Not implemented. Not sure how much we care about manipulating Rock Strata?
            //Array of arrays, not int
            //int[] newRockStrataData = new int[region.RockStrata.Length ^ 2];

            /// See above
            //int[] newShrubData = new int[region.ShrubMap.Size * region.ShrubMap.Size];

            /// Not sure what this map does?
            //ushort, not int
            //int[] newTerrainData = new int[region.TerrainMap.Size ^ 2];

            /// Also not sure what this really does
            //int[] newUpheavelData = new int[region.UpheavelMap.Size * region.UpheavelMap.Size];
            //newUpheavelData.Fill(255);
        }

        //Initialize and load the worldgen parameters
        private void InitRingedWorldGenerator()
        {
            /// First, check if this is a brand new world...
            //if (sapi.WorldManager.SaveGame.IsNew)
            {
                if (config.RingTemplates.Count > 0)
                {
                    foreach (var item in config.RingTemplates)
                    {
                        if (item.FromRing > item.ToRing)
                        {
                            sapi.Logger.Error($"FromRing was greater than ToRing for template: {item.Name}. Template will be ignored.");
                        }
                        else if (item.FromRing < 0 || item.ToRing >= NumberOfRings)
                        {
                            sapi.Logger.Error($"Ring range is out of bounds for template: {item.Name}. Template will be ignored.");
                        }
                        else if (item.FromRing == item.ToRing)
                        {
                            RingTemplates[item.FromRing] = item;
                        }
                        else if (item.ToRing > item.FromRing)
                        {
                            for (int i = item.FromRing; i <= item.ToRing; i++)
                            {
                                RingTemplates[i] = item;
                            }
                        }
                        else
                        {
                            sapi.Logger.Error($"Unhandled ring value case for template: {item.Name}. Template will be ignored.");
                        }
                    }

                    //this.RingTemplates = config.RingTemplates;
                }
                else
                {
                    CreateWorldgenValues();
                }
            }
            // if it isn't, just load what's already there (hopefully...)
            /*else
            {
                LoadWorldgenData();
            }*/
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
            int DeletionZoneWidthInRegions = ((toRing - fromRing) + 1) * ringWidth;

            /// Here we calculate the boundaries of the safezone.
            int FromOutsideSafezoneRegionXorZ = (int)(regionMidPoint - 0.5 - (safeZoneSize - 1));
            int ToOutsideSafezoneRegionXorZ = (int)(regionMidPoint + 0.5 + (safeZoneSize - 1));

            /// And here we adjust the starting ring number to be at least 1, because the previous calculation gives us the coordinates for the safezone.
            /// If we truly are deleting the safezone, we will add those coordinates into the deletion pool later on.
            int fromRingAdj = fromRing <= 0 ? 1 : fromRing;

            /// This is a square, so we can simplify the math by using the same calculations for X and Z. 
            /// We're calculating the inside boundary of the deletion zone.
            /// We start with the outermost region that is inside of the safezone and move one region outward in either direction.
            /// We take the adjusted starting ring number and subtract one because we're already in ring 1, so we need to offset ourselves.
            /// We then multiply by the ring width to get the total number of regions to move our **inside point** outward.
            int FromInsideRegionXorZ = (int)(FromOutsideSafezoneRegionXorZ - 1 - ((fromRingAdj - 1) * ringWidth));
            int ToInsideRegionXorZ = (int)(ToOutsideSafezoneRegionXorZ + 1 + ((fromRingAdj - 1) * ringWidth));

            /// Compute the maximum possible region coordinate. We are already assuming by the ring generator being active that the world is a square.
            int MaxRegionCoordinate = (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) - 1;

            /// Here we're taking the inside bound and adding in the size of the deletion zone in regions
            /// to create an outside bound. Note the - 1 -- We want to be on the inside edge of the outermost bound, not
            /// the outside edge, or else we'll encroach on the next ring outward (or go outside of the map).
            /// The conditionals give us guardrails in case the ring map size is not divisible by the safezone size and ring size together 
            /// (e.g., safezone size of 2 and ring size of 2).
            /// In this case, we could accidentally pass in region coordinates that don't exist.
            int FromOutsideRegionXorZ = (int)(FromInsideRegionXorZ - (DeletionZoneWidthInRegions - 1)) < 0 ? 0 : (int)(FromInsideRegionXorZ - (DeletionZoneWidthInRegions - 1));
            int ToOutsideRegionXorZ = (int)(ToInsideRegionXorZ + (DeletionZoneWidthInRegions - 1)) > MaxRegionCoordinate ? MaxRegionCoordinate : (int)(ToInsideRegionXorZ + (DeletionZoneWidthInRegions - 1));

            /// Note this will only trigger if 0 gets passed through. This is usually impossible, so we must REALLY mean it!
            if (fromRing == 0)
            {
                for (int i = FromOutsideSafezoneRegionXorZ; i <= ToOutsideSafezoneRegionXorZ; i++)
                {
                    for (int j = FromOutsideSafezoneRegionXorZ; j <= ToOutsideSafezoneRegionXorZ; j++)
                    {
                        regionCoordsToDelete.Add(new Vec2i(i, j));
                    }
                }
            }

            /// Note less or equal == we want to include the regions along the "to" coordinate
            /// This gets the largest sections of the zone to delete.
            for (int i = FromOutsideRegionXorZ; i <= ToOutsideRegionXorZ; i++) 
            {
                for (int j = FromOutsideRegionXorZ; j <= FromInsideRegionXorZ; j++)
                {
                    regionCoordsToDelete.Add(new Vec2i(i, j));
                }

                for (int j = ToInsideRegionXorZ; j <= ToOutsideRegionXorZ; j++) 
                {
                    regionCoordsToDelete.Add(new Vec2i(i, j));
                }
            }

            /// Here we get the remaining "slices" in the middle of the area.
            for (int i = FromInsideRegionXorZ + 1; i < ToInsideRegionXorZ; i++) 
            {
                for (int j = FromOutsideRegionXorZ; j <= FromInsideRegionXorZ; j++)
                {
                    regionCoordsToDelete.Add(new Vec2i(j, i));
                }

                for (int j = ToInsideRegionXorZ; j <= ToOutsideRegionXorZ; j++)
                {
                    regionCoordsToDelete.Add(new Vec2i(j, i));
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
            return;
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
            //RandomizeRingRange(fromRing, toRing, EnumDistribution.UNIFORM);
            //StoreWorldgenData();
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

                        bool allmax = ((Func<bool>)(() =>
                        {
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

                    .BeginSubCommand("blockentity")
                    .WithArgs(sapi.ChatCommands.Parsers.WorldPosition("position"))
                    .HandleWith((args) =>
                    {

                        Vec3d pos = (Vec3d)(args[0]);

                        var be = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(new BlockPos((int)pos.X, (int)pos.Y, (int)pos.Z));
                        sapi.World.BlockAccessor.RemoveBlockEntity(be.Pos);
                        be.MarkDirty(true);

                        return TextCommandResult.Success("delete sumn");
                    })
                    .EndSubCommand()

                .EndSubCommand()
                .BeginSubCommand("repair")
                    .BeginSubCommand("blockentities")
                    .WithArgs(sapi.ChatCommands.Parsers.WorldPosition("frompos"), sapi.ChatCommands.Parsers.WorldPosition("topos"))
                    .HandleWith((args) =>
                    {
                        string output = "";

                        Vec3d fromPosd = (Vec3d)args[0];
                        Vec3i fromPos = new Vec3i((int)fromPosd.X, (int)fromPosd.Y, (int)fromPosd.Z);

                        Vec3d toPosd = (Vec3d)args[1];
                        Vec3i toPos = new Vec3i((int)toPosd.X, (int)toPosd.Y, (int)toPosd.Z);

                        if (fromPos.X > toPos.X)
                        {
                            (fromPos.X, toPos.X) = (toPos.X, fromPos.X);
                        }
                        if (fromPos.Y > toPos.Y)
                        {
                            (fromPos.Y, toPos.Y) = (toPos.Y, fromPos.Y);
                        }
                        if (fromPos.Z > toPos.Z)
                        {
                            (fromPos.Z, toPos.Z) = (toPos.Z, fromPos.Z);
                        }



                        for (int x = fromPos.X; x <= toPos.X; x++)
                        {
                            for (int y = fromPos.Y; y <= toPos.Y; y++)
                            {
                                for (int z = fromPos.Z; z <= toPos.Z; z++)
                                {
                                    BlockPos targetpos = new BlockPos(x, y, z);
                                    var targetblock = sapi.World.BlockAccessor.GetBlock(targetpos);

                                    if (
                                        targetblock.Code.Domain == "rustwall" && 
                                        (targetblock.BlockBehaviors.ToList().Find(item => item.GetType() == typeof(BehaviorRebuildable)) as BehaviorRebuildable) is not null &&
                                        (sapi.World.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(targetpos) is null)
                                        )
                                    {
                                        output += "Found " + targetblock.Code + " at " + targetpos + "\n";

                                        sapi.World.BlockAccessor.SetBlock(0, targetpos);
                                        sapi.World.BlockAccessor.SetBlock(targetblock.Id, targetpos);

                                        var maybefixedbe = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(targetpos);

                                        if (maybefixedbe is not null)
                                        {
                                            output += "Successfully repaired BERebuildable at " + targetpos + "\n";
                                            maybefixedbe.MarkDirty(true);
                                        }
                                    }
                                }
                            }
                        }
                        return TextCommandResult.Success(output);
                    })
                    .EndSubCommand()
                    .BeginSubCommand("reload")
                        .BeginSubCommand("config")
                        .WithArgs()
                        .HandleWith(
                        (args =>
                        {
                            var rwmodsys = sapi.ModLoader.GetModSystem<RustwallModSystem>();

                            rwmodsys.ReloadConfig();

                            return TextCommandResult.Success("Reloaded Rustwall configuration");
                        }))
                        .EndSubCommand()
                    .EndSubCommand()
                .EndSubCommand();
        }
    }
}

