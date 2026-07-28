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
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Rustwall.ModSystems.RingedGenerator
{
    //[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
    public class RingedGeneratorSystem : RustwallModSystem
    {
        public enum EnumWorldGenParameters
        {
            landformScale,
            globalTemperature,
            globalPrecipitation,
            globalForestation,
            landcover,
            oceanscale,
            upheavelCommonness,
            geologicActivity
        }
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
        private List<double> WorldgenDefaultParams { get; set; } = new List<double> { 1, 1, 1, 0, 0.975, 1, 0.3, 0.05 };
        //private static int curRing = 0; 
        //private static int desiredRing = 0;
        private double regionMidPoint;
        GenMaps mapGenerator { get; set; }
        /// <summary>
        /// Unused right now, might be useful later if I want to get deeper into the ore or clay generation weeds
        /// </summary>
        //GenDeposits depositGenerator { get; set; }
        public int LeftOverRings { get; private set; }
        public Dictionary<int, RingData> FinalRingDataForRealThisTime = [];

        /// <summary>
        /// Ensures that our mod registers its Region generation handlers *after* the vanilla worldgen handlers
        /// </summary>
        /// <returns></returns>
        public override double ExecuteOrder()
        {
            return 1;
        }

        protected override void RustwallStartServerSide()
        {
            mapGenerator = sapi.ModLoader.GetModSystem<GenMaps>();
            RegisterChatCommands();

            sapi.Event.ServerRunPhase(EnumServerRunPhase.WorldReady, () => 
            {
                ringWidth = config.RingWidth;
                safeZoneSize = config.SafeZoneSize;
                int RegionMapSizeX = -1;

                /// This calculates map size relative to the resolution of the rings
                /// It also checks to make sure the world is a square; if it is rectangular, the ring generator doesn't initialize
                if (sapi.WorldManager.MapSizeX == sapi.WorldManager.MapSizeZ)
                {
                    RegionMapSizeX = (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) / 2;
                    int RegionMapSizeXWithoutSafeZone = RegionMapSizeX - safeZoneSize;
                    LeftOverRings = RegionMapSizeXWithoutSafeZone % ringWidth;
                    /// This should theoretically always leave an even division of the RegionMap into rings and account for that extra territory at the edge.
                    NumberOfRings = LeftOverRings == 0 ? 
                        /// We want the +1 to account for the safezone that we shaved off earlier.
                        (RegionMapSizeXWithoutSafeZone / ringWidth) + 1 
                        : 
                        /// Here, we need +2; we need to account for the safezone as before, and also a bonus ring for the remainder.
                        ((RegionMapSizeXWithoutSafeZone - LeftOverRings) / ringWidth) + 2;
                }
                else 
                {
                    NumberOfRings = -500;
                }

                regionMidPoint = ((RegionMapSizeX + RegionMapSizeX - 1) / 2.0);
            });

            sapi.Event.InitWorldGenerator(() => InitRingedWorldGenerator(false), "standard");

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

            if (!FinalRingDataForRealThisTime.TryGetValue(ringNum, out RingData ringData))
            {
                return;
            }

            var template = ringData.template;
            var inputParams = ringData.regionMapLayerGenerators;

            int[] newBeachData = new int[region.BeachMap.Size * region.BeachMap.Size];

            if (template.beachData > -1)
            {    
                newBeachData.Fill(template.beachData);
            }
            else
            {
                newBeachData = inputParams.GenMaps_beachGen.GenLayer(
                    regionX * mapGenerator.noiseSizeBeach,
                    regionZ * mapGenerator.noiseSizeBeach,
                    mapGenerator.noiseSizeBeach + 1,
                    mapGenerator.noiseSizeBeach + 1
                );
            }

            region.BeachMap.Data = newBeachData;

            /// Not sure what BiomeData represents
            //int[] newBiomeData = new int[region.BiomeMap.Size * region.BiomeMap.Size];

            /// Blockpatches seem to mostly govern things like shrubs and mushrooms. Not sure 
            /// I care that much about this (could I make LSD land...?)
            //Dictionary
            //int[] newBlockPatchData = new int[region.BlockPatchMaps.Size ^ 2];

            ///Some custom methods for handling the binary packing
            static int PackClimate(int rainfall, int temperature)
            {
                int result = (rainfall & 0xFF) << 8 | ((temperature & 0xFF) << 16);
                return result;
            }

            static int UnpackRainfall(int packedClimate)
            {
                return (packedClimate & 0x00FF00) >> 8;
            }

            static int UnpackTemperature(int packedClimate)
            {
                return (packedClimate & 0xFF0000) >> 16;
            }

            int[] newClimateData = new int[region.ClimateMap.Size * region.ClimateMap.Size];

            /// If both rainfall and temperature are specified in the template, 
            /// we can just fill the climate map with that data.
            if (template.rainfallData > -1 && template.temperatureData > -1)
            {
                newClimateData.Fill(PackClimate(template.rainfallData, template.temperatureData));
            }
            /// If only one is provided, pull the existing data out of the map and overwrite the provided
            /// data in the region data.
            else if (template.rainfallData > -1 && template.temperatureData <= -1)
            {
                for (int i = 0; i < region.ClimateMap.Data.Length; i++)
                {
                    int temp = UnpackTemperature(region.ClimateMap.Data[i]);
                    int packedClimate = PackClimate(template.rainfallData, temp);
                    newClimateData[i] = packedClimate;
                }
            }
            else if (template.rainfallData <= -1 && template.temperatureData > -1)
            {
                for (int i = 0; i < region.ClimateMap.Data.Length; i++)
                {
                    int rainfall = UnpackRainfall(region.ClimateMap.Data[i]);
                    int packedClimate = PackClimate(rainfall, template.temperatureData);
                    newClimateData[i] = packedClimate;
                }
            }
            /// and if neither is provided, just do it normally!
            else
            {
                int pad = 2;
                newClimateData = inputParams.GenMaps_climateGen.GenLayer(
                    regionX * mapGenerator.noiseSizeClimate - pad,
                    regionZ * mapGenerator.noiseSizeClimate - pad,
                    mapGenerator.noiseSizeClimate + 2 * pad,
                    mapGenerator.noiseSizeClimate + 2 * pad
                );
            }

            region.ClimateMap.Data = newClimateData;

            var newForestData = new int[region.ForestMap.Size * region.ForestMap.Size];
            /// This needs some edits... should not be putting in the current ClimateMap, we need to use the modified one.
            if (template.forestData > -1)
            {
                newForestData.Fill(template.forestData);
            }
            else
            {
                inputParams.GenMaps_forestGen.SetInputMap(region.ClimateMap, region.ForestMap);
                newForestData = inputParams.GenMaps_forestGen.GenLayer(
                    regionX * mapGenerator.noiseSizeForest,
                    regionZ * mapGenerator.noiseSizeForest,
                    mapGenerator.noiseSizeForest + 1,
                    mapGenerator.noiseSizeForest + 1
                );
            }

            region.ForestMap.Data = newForestData;

            /// Not yet implemented and I don't know what it does
            /// I think geoprovs have to do with what rock types can generate and how the terrain is shaped...?
            //int[] newGeoProvData = new int[region.GeologicProvinceMap.Size * region.GeologicProvinceMap.Size];
            /*if (template.geoprovData > -1)
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
            }*/



            var newLandformData = new int[region.LandformMap.Size * region.LandformMap.Size];

            if (template.landformData is not null && template.landformData != "")
            {
                //int[] newLandformData = new int[mapRegion.LandformMap.Size * mapRegion.LandformMap.Size];
                string desiredLandform = template.landformData;
                int landformCode = NoiseLandforms.landforms.GetIndexByCode(desiredLandform);
                if (landformCode != -1)
                {
                    newLandformData.Fill(landformCode);
                }
                else
                {
                    sapi.Logger.Error($"Failed to find landform code for {desiredLandform}. Landform map will be unaltered.");
                }
            }
            else
            {
                int pad = TerraGenConfig.landformMapPadding;
                newLandformData = inputParams.GenMaps_landformsGen.GenLayer(
                    regionX * mapGenerator.noiseSizeLandform - pad,
                    regionZ * mapGenerator.noiseSizeLandform - pad,
                    mapGenerator.noiseSizeLandform + 2 * pad,
                    mapGenerator.noiseSizeLandform + 2 * pad);
            }

            region.LandformMap.Data = newLandformData;

            /// -1 is the default value, which means "don't change it"

            int[] newOceanData = new int[region.OceanMap.Size * region.OceanMap.Size];

            if (template.oceanData > -1)
            {
                newOceanData.Fill(template.oceanData);
            }
            else
            {
                int opad = 5;
                newOceanData = inputParams.GenMaps_oceanGen.GenLayer(
                    regionX * mapGenerator.noiseSizeOcean - opad,
                    regionZ * mapGenerator.noiseSizeOcean - opad,
                    mapGenerator.noiseSizeOcean + 2 * opad,
                    mapGenerator.noiseSizeOcean + 2 * opad
                );
            }

            region.OceanMap.Data = newOceanData;

            /// Not used right now. Not really sure how much I care about putting these in.
            //int[] newOreVerticalDistortBottomData = new int[region.OreMapVerticalDistortBottom.Size * region.OreMapVerticalDistortBottom.Size];
            //int[] newOreVerticalDistortTopData = new int[region.OreMapVerticalDistortBottom.Size * region.OreMapVerticalDistortBottom.Size];

            /// We'll fill if we have template data, otherwise we'll just skip it and let the default
            /// deposit generator run
            if (template.oreData != null)
            {
                static int PackOreValues(OreValues values)
                {
                    return (values.value & 0xFF) | ((values.hypercommonness & 0xFF) << 8) | ((values.richness & 0xFF) << 16);
                }

                foreach (var kvp in template.oreData)
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

            region.DirtyForSaving = true;
        }

        //Initialize and load the worldgen parameters
        private void InitRingedWorldGenerator(bool flushCache)
        {
            /// flushCache is used by the GreatDecay methods to instruct InitRingedWorldGenerator to fire even on an already-playing world.
            /// This will erase the current world generation options from the savegame and ensure they are up-to-date.
            if (sapi.WorldManager.SaveGame.IsNew || flushCache)
            {
                if (config.RingTemplates.Count > 0)
                {
                    foreach (var item in config.RingTemplates)
                    {
                        if (item.FromRing > item.ToRing)
                        {
                            sapi.Logger.Error($"FromRing was greater than ToRing for template: {item.Name}. Template will be ignored.");
                        }
                        else if (item.FromRing < 0 || item.ToRing >= NumberOfRings - 1)
                        {
                            sapi.Logger.Error($"Ring range is out of bounds for template: {item.Name}. Template will be ignored.");
                        }
                        else if (item.FromRing == item.ToRing)
                        {
                            int inputSeed = item.seed <= 0 ? sapi.WorldManager.SaveGame.Seed : item.seed;
                            FinalRingDataForRealThisTime[item.FromRing] = new RingData(sapi, inputSeed, item);
                        }
                        else if (item.ToRing > item.FromRing)
                        {
                            int inputSeed = item.seed <= 0 ? sapi.WorldManager.SaveGame.Seed : item.seed;
                            for (int i = item.FromRing; i <= item.ToRing; i++)
                            {
                                FinalRingDataForRealThisTime[i] = new RingData(sapi, inputSeed, item);
                            }
                        }
                        else
                        {
                            sapi.Logger.Error($"Unhandled ring value case for template: {item.Name}. Template will be ignored.");
                        }
                    }
                }

                BackfillRandomWorldgenValues();

                StoreWorldgenData();
            }
            else
            {
                LoadWorldgenData();
            }
        }

        /// <summary>
        /// Checks for gaps in the ring data list and fills them in using new templates with randomized values.
        /// </summary>
        private void BackfillRandomWorldgenValues()
        {
            List<int> KeysToRandomize = [];

            int lastKey = -1;
            /// Compares each key in the table with the previous; will catch gaps where templates were not provided.
            foreach (var kvp in FinalRingDataForRealThisTime)
            {
                if (kvp.Key - lastKey > 1)
                {
                    for (int i = lastKey + 1; i < kvp.Key; i++)
                    {
                        KeysToRandomize.Add(i);
                    }
                }
                lastKey = kvp.Key;
            }

            /// The previous eval only checks for gaps in between provided values, but we also need to make sure there's no rings missing at the edge.
            if (lastKey < NumberOfRings - 1)
            {
                for (int i = lastKey + 1; i < NumberOfRings; i++)
                {
                    KeysToRandomize.Add(i);
                }
            }

            foreach (int key in KeysToRandomize)
            {
                RandomizeParams(key);
            }
        }

        private void StoreWorldgenData()
        {
            var templateList = new Dictionary<int, RGWorldgenTemplate>();
            foreach (var kvp in FinalRingDataForRealThisTime)
            {
                templateList[kvp.Key] = kvp.Value.template;

                //sapi.WorldManager.SaveGame.StoreData("rustwallRingData_" + kvp.Key, SerializerUtil.Serialize(kvp.Value.template));
                sapi.WorldManager.SaveGame.StoreData("rustwallRingData", SerializerUtil.Serialize(templateList));
            }
        }

        private void LoadWorldgenData()
        {
            var retrievedTemplates = sapi.WorldManager.SaveGame.GetData<Dictionary<int, RGWorldgenTemplate>>("rustwallRingData");
            if (retrievedTemplates is not null)
            {
                foreach (var kvp in retrievedTemplates)
                {
                    FinalRingDataForRealThisTime.Add(kvp.Key, new RingData(sapi, kvp.Value.seed, kvp.Value));
                }
            }
            else
            {
                /// TODO: CreateWorldgenValues needs a refactor still
                sapi.Logger.Error($"Ring data was not found in savegame. Initializing new dataset from current configuration.");
                BackfillRandomWorldgenValues();
            }
        }

        private void RandomizeParams(int ring)
        {
            Dictionary<EnumWorldGenParameters, float> newParams = new();

            //var WorldgenMinParams = new List<float> { 0.5f, 0, 0, -1, 0.1f, 0.1f, 0, 0 };
            //var WorldgenMaxParams = new List<double> { 1.5, 5, 5, 1, 1, 4, 1, 0.4 };
            var WorldgenAverageParams = new List<float> { 1, 2.5f, 2.5f, 0, 0.55f, 2.05f, 0.5f, 0.2f };
            var WorldgenVarianceParams = new List<float> { .5f, 2.5f, 2.5f, 1, 0.45f, 1.95f, 0.5f, 0.2f };
            
            foreach (var wgparam in Enum.GetValues<EnumWorldGenParameters>())
            {
                var natfl = NatFloat.create((EnumDistribution)config.RandomizationDistribution, WorldgenAverageParams[(int)wgparam], WorldgenVarianceParams[(int)wgparam]);
                newParams.Add(wgparam, natfl.nextFloat());
            }

            Random rand = new Random();
            int newSeed = sapi.WorldManager.Seed + rand.Next(10000);
            FinalRingDataForRealThisTime[ring] = new RingData
            (
                sapi,
                newSeed,
                new RGWorldgenTemplate()
                {
                    seed = newSeed,
                    landformScale = newParams[EnumWorldGenParameters.landformScale],
                    globalTemperature = newParams[EnumWorldGenParameters.globalTemperature],
                    globalPrecipitation = newParams[EnumWorldGenParameters.globalPrecipitation],
                    globalForestation = newParams[EnumWorldGenParameters.globalForestation],
                    landcover = newParams[EnumWorldGenParameters.landcover],
                    oceanscale = newParams[EnumWorldGenParameters.oceanscale],
                    upheavelCommonness = newParams[EnumWorldGenParameters.upheavelCommonness],
                    geologicActivity = newParams[EnumWorldGenParameters.geologicActivity],
                }
            );
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
                /// This is a hacky way to bypass players getting stuck in an ungenerated part of the world.
                /// We just teleport them to their current position; the game registers this as movement and starts feeding
                /// new chunks to the player.
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
        
        public void TriggerGreatDecay(int fromRing, int toRing, bool flushCache)
        {
            //We are not allowed to regen ring 0 (the innermost safe zone). This hardcodes that in even if players let the stability get to 0
            if (fromRing <= 0)
            {
                sapi.Logger.Error("Rustwall error: fromRing was less than or equal to 0. Safezone deletions are forbidden. Changing to 1.");
                fromRing = 1;
            }

            if (toRing <= 0)
            {
                sapi.Logger.Error("Rustwall error: toRing was less than or equal to 0. Safezone deletions are forbidden. Changing to 1.");
                toRing = 1;
            }

            if (toRing > NumberOfRings)
            {
                sapi.Logger.Error("Rustwall error: requested deletion exceeds size of ring map. Try a smaller value.");
                return;
            }

            if (fromRing > toRing)
            {
                sapi.Logger.Error("Rustwall error: fromRing was greater than toRing. What the fuck did you do?");
                return;
            }

            if (fromRing >= NumberOfRings)
            {
                fromRing = NumberOfRings - 1;
                sapi.Logger.Error("Rustwall error: fromRing exceeded size of ring map. This will crash the fuck out of the server");
            }

            if (toRing >= NumberOfRings)
            {
                toRing = NumberOfRings - 1;
                sapi.Logger.Error("Rustwall error: toRing exceeded size of ring map. This will crash the fuck out of the server");
            }

            StopChunkGeneration();
            if (flushCache)
            {
                InitRingedWorldGenerator(flushCache);
            }
            DeleteRingRange(fromRing, toRing);
            StartChunkGeneration();
        }

        public void TriggerGreatDecay(float stabRatio, bool flushCache)
        {
            int fromRing = (int)(NumberOfRings - (NumberOfRings * stabRatio));
            int toRing = NumberOfRings;

            TriggerGreatDecay(fromRing, toRing, flushCache);
        }

        public void TriggerGreatDecay(int ring, bool flushCache)
        {
            TriggerGreatDecay(ring, ring, flushCache);
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
                    /*.BeginSubCommand("params")
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
                    .EndSubCommand()*/
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
                    .WithArgs(sapi.ChatCommands.Parsers.Int("ring"), sapi.ChatCommands.Parsers.Bool("flushCache"))
                    .HandleWith((args) =>
                    {
                        TriggerGreatDecay((int)args[0], (bool)args[1]);

                        string flushed = (bool)args[1] ? " and flushed the saved ring generator" : "";

                        string output = $"Deleted ring {(int)args[0]}" + flushed;

                        return TextCommandResult.Success(output);
                    })
                    .EndSubCommand()

                    .BeginSubCommand("ringrange")
                    .WithArgs(sapi.ChatCommands.Parsers.Int("fromRing"), sapi.ChatCommands.Parsers.Int("toRing"), sapi.ChatCommands.Parsers.Bool("flushCache"))
                    .HandleWith((args) =>
                    {
                        TriggerGreatDecay((int)args[0], (int)args[1], (bool)args[2]);

                        string flushed = (bool)args[2] ? " and flushed the saved ring generator" : "";

                        string output = $"Deleted rings {(int)args[0]} through {(int)args[1]}" + flushed;

                        return TextCommandResult.Success(output);
                    })
                    .EndSubCommand()

                    .BeginSubCommand("ratio")
                    .WithArgs(sapi.ChatCommands.Parsers.Float("ratio"), sapi.ChatCommands.Parsers.Bool("flushCache"))
                    .HandleWith((args) =>
                    {
                        TriggerGreatDecay((float)args[0], (bool)args[1]);

                        string flushed = (bool)args[1] ? " and flushed the saved ring generator" : "";

                        string output = $"Deleted rings using a ratio of {(int)args[1]}%" + flushed;

                        return TextCommandResult.Success(output);
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

                        return TextCommandResult.Success($"Erased block entity at position {(int)pos.X}, {(int)pos.Y}, {(int)pos.Z}");
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
                .EndSubCommand();
        }
    }
}

