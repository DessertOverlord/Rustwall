using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.ServerMods.NoObf;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.Server;

namespace Rustwall.ModSystems.RingedGenerator
{
    internal class RingedGeneratorSystem : RustwallModSystem
    {
        //ICoreServerAPI sapi;
        // ringsize must be an even number (? haven't tried an odd number yet) and determines how wide each ring is.
        private static int ringSize = 2;
        // each dictionary holds the parameters for one ring's worldgen. organized into a list for scalability
        private List<Dictionary<string, double>> ringDictList { get; set; }
        // seedlist performs the same thing as above, just holding all of the seeds.
        private List<int> seedList { get; set; } = new List<int>() ;
        // this list is all of the settings we want to mess with. Can be added to easily.
        private readonly List<string> WorldgenParamsToScramble = new List<string> { "landformScale", "globalTemperature", "globalPrecipitation", "globalForestation", "landcover", "oceanscale", "upheavelCommonness", "geologicActivity" };
        // The default parameters for each of the associated parameters to scramble. ORDER MATTERS!
        // Some day I won't have to do this, but I haven't figured out how to gather the currently selected params until
        // after the game is saved for the first time.
        // TODO: programmatically gather the selected worldgen params on first launch.
        private readonly List<double> WorldgenDefaultParams = new List<double> { 1, 1, 1, 0, 1, 1, 0.3, 0.05 };
        private static int curRing = 0;
        private static int desiredRing = 0;
        private static int ringMapSize;
        private double regionMidPoint;

        protected override void RustwallStartServerSide()
        {
            RegisterChatCommands();
            // This calculates map size relative to the resolution of the rings
            // It also checks to make sure the world is a square; if it is rectangular, the ring generator doesn't initialize
            ringMapSize = sapi.WorldManager.MapSizeX == sapi.WorldManager.MapSizeZ ? (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) / 2 : -500;
            regionMidPoint = ((ringMapSize + ringMapSize - 1) / 2.0);
            ringDictList = new List<Dictionary<string, double>>(ringMapSize);

            InitRingedWorldGenerator();

            //Add the region method to the MapRegionGeneration event, causing it to be called any time the engine wants to generate a new region
            MapRegionGeneratorDelegate regionHandler = HandleRegionLoading;
            sapi.Event.MapRegionGeneration(regionHandler, "standard");

            //Add the chunk method to MapChunkGeneration; this is triggered any time a new chunk column is requested.
            MapChunkGeneratorDelegate chunkHandler = HandleChunkLoading;
            sapi.Event.MapChunkGeneration(chunkHandler, "standard");
        }

        private void HandleChunkLoading(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            if (ringMapSize == -500) { return; }
            int regionX = chunkX / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
            int regionZ = chunkZ / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
            //HandleChunkLoading would perform the same math... so we just figure out what region the chunk is in and call HandleRegionLoading!
            HandleRegionLoading(null, regionX, regionZ);
        }

        private void HandleRegionLoading(IMapRegion region, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            
            if (ringMapSize == -500) { return; }
            desiredRing = (int)(double.Max(Math.Abs(regionX - regionMidPoint), Math.Abs(regionZ - regionMidPoint)) - 0.5);
            if (curRing != desiredRing)
            {
                curRing = desiredRing;
                SetWorldParams(sapi, ringDictList[curRing], seedList[curRing]);
                RestartChunkGenerator();
            }
        }

        //Initialize and load the worldgen parameters
        private void InitRingedWorldGenerator()
        {
            //If this is the first world load, we need some fresh params
            if (sapi.WorldManager.SaveGame.IsNew)
            {
                CreateWorldgenValues();
            }
            // if it isn't, just load what's already there (hopefully...)
            else
            {
                byte[] seedData = sapi.WorldManager.SaveGame.GetData("rustwallRingSeeds");
                //this happens if the world is improperly saved after the initial world load.
                //HOPEfully this should never arise.
                if (seedData != null)
                { 
                    seedList = SerializerUtil.Deserialize<List<int>>(seedData); 
                } 
                else 
                { 
                    CreateWorldgenValues(); 
                }

                for (int i = 0; i < ringMapSize; i++)
                {
                    byte[] data = sapi.WorldManager.SaveGame.GetData("rustwallRingData_" + i);
                    ringDictList.Add(SerializerUtil.Deserialize<Dictionary<string, double>>(data));
                }
            }
        }

        //Initialize first-time world generator values
        private void CreateWorldgenValues()
        {
            //Initialize seedList and ringDictList, and loop through them to populate values
            seedList.Add(sapi.WorldManager.SaveGame.Seed);
            ringDictList.Add(new Dictionary<string, double>());
            //This adds the default worldgen params to spawn (ring 0)
            for (int i = 0; i < WorldgenParamsToScramble.Count(); i++)
            {
                ringDictList[0].Add(WorldgenParamsToScramble[i], WorldgenDefaultParams[i]);
            }
            // and this adds the rest of them -- note -1 because we already added 1 with the previous loop
            // it needs to be less than or equal to because I want exactly 25 (minus the first one already added), not 24. Otherwise shit goes sideways!
            for (int i = 0; i <= ringMapSize - 1; i++)
            {
                RandomizeParams(out Dictionary<string, double> newParams, out int seed, EnumDistribution.NARROWINVERSEGAUSSIAN);
                seedList.Add(seed);
                ringDictList.Add(newParams);
            }

            StoreWorldgenData();
        }

        private void StoreWorldgenData()
        {
            // this stores the generated seeds and params into the savegame, making them persistent
            sapi.WorldManager.SaveGame.StoreData("rustwallRingSeeds", SerializerUtil.Serialize(seedList));
            for (int i = 0; i < ringDictList.Count; i++)
            {
                //note that each array must be saved separately -- nested arrays of different sizes are not supported for serialization
                sapi.WorldManager.SaveGame.StoreData("rustwallRingData_" + i, SerializerUtil.Serialize(ringDictList[i]));
            }
        }

        //RandomDoubleInRange does what it says, giving a random double between minVal and maxVal.
        // TODO: maybe add distributions to weight the results?
        // DONE: Can now supply RandomizeParams with a distribution type to mess with how it chooses values.
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
            newSeed = sapi.World.Seed + sapi.World.Rand.Next(100000);
        }

        private void RandomizeRing(int ringNumber, EnumDistribution dist = EnumDistribution.NARROWINVERSEGAUSSIAN)
        {
            RandomizeParams(out Dictionary<string, double> newParams, out int newSeed, dist);

            ringDictList[ringNumber].Clear();
            ringDictList[ringNumber].AddRange(newParams);

            seedList[ringNumber] = newSeed;

        }

        private void RandomizeRingRange(int fromRing, int toRing, EnumDistribution dist = EnumDistribution.NARROWINVERSEGAUSSIAN)
        {
            for (int i = fromRing; i < toRing; i++)
            {
                RandomizeRing(i, dist);
            }
        }

        //SetWorldParams takes the parameters and seed provided and updates the world generator with them.
        private void SetWorldParams(ICoreServerAPI api, Dictionary<string, double> keyValuePairs, int seed)
        {
            foreach (var item in keyValuePairs)
            {
                sapi.World.Config.SetDouble(item.Key, item.Value);
                sapi.World.Config.TryGetAttribute(item.Key, out IAttribute curAttr);
            }
            sapi.WorldManager.SaveGame.Seed = seed;
        }

        //Turns off chunk generation and sending to clients, reloads all of the worldgen parameters (seed, multipliers), and then re-enables everything.
        // Necessary any time we change what ring we're generating.
        private void RestartChunkGenerator()
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
        }

        private void StopChunkGeneration()
        {
            sapi.WorldManager.AutoGenerateChunks = false;
            sapi.WorldManager.SendChunks = false;
        }

        private void ReloadWorldgenAssets()
        {
            sapi.Assets.Reload(new AssetLocation("worldgen/"));
            var patchLoader = sapi.ModLoader.GetModSystem<ModJsonPatchLoader>();
            patchLoader.ApplyPatches("worldgen/");
            sapi.Event.TriggerInitWorldGen();
        }

        private void StartChunkGeneration()
        {
            sapi.WorldManager.AutoGenerateChunks = true;
            sapi.WorldManager.SendChunks = true;
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
                for (int j = fromRegionX; j < toRegionX; j++)
                {
                    regionCoordsToDelete.Add(new Vec2i(j, fromRegionZ));
                    regionCoordsToDelete.Add(new Vec2i(j, toRegionZ));
                }
                for (int j = fromRegionZ; j < toRegionZ; j++)
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
                        //Debug.WriteLine("I would like to delete the chunk at: " + j + ", " + k);
                        sapi.WorldManager.DeleteChunkColumn(j, k);
                    }
                }
                //Debug.WriteLine("I would like to delete the region at: " + i.X + ", " + i.Y);
            }
        }

        public void Apocalypse(float stabRatio)
        {
            int ringsToDelete = (int)(ringMapSize - (ringMapSize * stabRatio));
            int fromRing = ringsToDelete;
            int toRing = ringMapSize;

            //We are not allowed to regen ring 0 (the innermost safe zone). This hardcodes that in even if players let the stability get to 0
            if (fromRing == 0) { fromRing = 1; }

            StopChunkGeneration();
            RandomizeRingRange(fromRing, toRing);
            StoreWorldgenData();
            DeleteRingRange(fromRing, toRing);
            ReloadWorldgenAssets();
            StartChunkGeneration();
        }

        private void RegisterChatCommands()
        {
            sapi.ChatCommands.Create("regreg")
                .WithDescription("I UNNO")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .WithArgs()
                .HandleWith((args) =>
                {

                    Debug.WriteLine("Attempting to pause and reload chunk generation");

                    if (sapi.Server.PauseThread("chunkdbthread"))
                    {
                        IServerPlayer player = (IServerPlayer)args.Caller.Player;
                        sapi.Assets.Reload(new AssetLocation("worldgen/"));
                        var patchLoader = sapi.ModLoader.GetModSystem<ModJsonPatchLoader>();
                        patchLoader.ApplyPatches("worldgen/");

                        sapi.Event.TriggerInitWorldGen();

                        var regionVec3Pos = args.Caller.Player.WorldData.EntityPlayer.Pos.XYZInt;
                        int regionX = regionVec3Pos.X / sapi.WorldManager.RegionSize;
                        int regionZ = regionVec3Pos.Z / sapi.WorldManager.RegionSize;
                        List<Vec2i> coords = new List<Vec2i>();
                        int chunksInRegion = (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
                        int chunkX = regionX * chunksInRegion;
                        int chunkZ = regionZ * chunksInRegion;

                        for (int x = chunkX; x < (chunkX + chunksInRegion); x++)
                        {
                            for (int z = chunkZ; z < (chunkZ + chunksInRegion); z++)
                            {
                                coords.Add(new Vec2i(x, z));
                            }
                        }

                        foreach (Vec2i coord in coords)
                        {
                            sapi.WorldManager.DeleteChunkColumn(coord.X, coord.Y);
                        }
                        sapi.WorldManager.DeleteMapRegion(regionX, regionZ);

                        int leftToLoad = coords.Count;
                        bool sent = false;
                        sapi.WorldManager.SendChunks = false;

                        foreach (Vec2i coord in coords)
                        {
                            sapi.WorldManager.LoadChunkColumnPriority(coord.X, coord.Y, new ChunkLoadOptions()
                            {
                                OnLoaded = () =>
                                {
                                    leftToLoad--;

                                    if (leftToLoad <= 0 && !sent)
                                    {
                                        sent = true;

                                        player.CurrentChunkSentRadius = 0;
                                        sapi.WorldManager.SendChunks = true;

                                        foreach (Vec2i ccoord in coords)
                                        {
                                            for (int cy = 0; cy < sapi.WorldManager.MapSizeY / GlobalConstants.ChunkSize; cy++)
                                            {
                                                sapi.WorldManager.BroadcastChunk(ccoord.X, cy, ccoord.Y, true);
                                            }
                                        }
                                    }
                                },
                            });
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Unable to pause chunk generation");
                    }

                    sapi.Server.ResumeThread("chunkdbthread");

                    return TextCommandResult.Success("deleted some stuff?");
                });

            sapi.ChatCommands.Create("drr")
                .WithDescription("I UNNO")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .WithArgs()
                .HandleWith((args) =>
                {
                    //RandomizeParams(out Dictionary<string, double> newParams, out int newSeed, EnumDistribution.NARROWINVERSEGAUSSIAN);
                    Apocalypse(1.0f);
                   

                    return TextCommandResult.Success("deleted some shit prolly");
                });

        }
    }
}

