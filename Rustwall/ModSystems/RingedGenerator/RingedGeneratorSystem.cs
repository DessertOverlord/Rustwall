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

namespace Rustwall.ModSystems.RingedGenerator
{
    internal class RingedGeneratorSystem : RustwallModSystem
    {
        ICoreServerAPI sapi;
        // ringsize must be an even number (? haven't tried an odd number yet) and determines how wide each ring is.
        private static int ringSize = 2;
        // each dictionary holds the parameters for one ring's worldgen. organized into a list for scalability
        private List<Dictionary<string, double>> ringDictList = new List<Dictionary<string, double>>();
        // seedlist performs the same thing as above, just holding all of the seeds.
        private List<int> seedList = new List<int>();
        // this list is all of the settings we want to mess with. Can be added to easily.
        private readonly List<string> WorldgenParamsToScramble = new List<string> { "landformScale", "globalTemperature", "globalPrecipitation", "globalForestation", "landcover", "oceanscale", "upheavelCommonness", "geologicActivity" };
        // The default parameters for each of the associated parameters to scramble. ORDER MATTERS!
        // Some day I won't have to do this, but I haven't figured out how to gather the currently selected params until
        // after the game is saved for the first time.
        // TODO: programmatically gather the selected worldgen params on first launch.
        private readonly List<double> WorldgenDefaultParams = new List<double> { 1, 1, 1, 0, 1, 1, 0.3, 0.05 };
        public static int curRing { get; private set; } = 0;
        public static int desiredRing { get; private set; } = 0;

        protected override void RustwallStartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            RegisterChatCommands();

            // First, we need to store the current seed, for generating the "ring 0" safe zone.
            int seed;
            // This calculates down to the resolution of rings.
            int ringMapSize = sapi.WorldManager.MapSizeX == sapi.WorldManager.MapSizeZ ? (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) / 2 : -500;
            // Add the first, initial seed to the seedList.
            seedList.Add(sapi.WorldManager.SaveGame.Seed);
            //If the ringdictionary is empty, add the initial worldgen params, and then add the randomized ones.
            if (ringDictList.Count == 0)
            {
                ringDictList.Add(new Dictionary<string, double>());
                for (int i = 0; i < WorldgenParamsToScramble.Count(); i++)
                {
                    ringDictList[0].Add(WorldgenParamsToScramble[i], WorldgenDefaultParams[i]);
                }
                for (int i = 0; i < ringMapSize - 1; i++)
                {
                    RandomizeParams(sapi, out Dictionary<string, double> newParams, out seed, EnumDistribution.NARROWINVERSEGAUSSIAN);
                    seedList.Add(seed);
                    ringDictList.Add(newParams);
                }
            }

            double regionMidPoint = ((ringMapSize + ringMapSize - 1) / 2.0);
            void HandleRegionLoading(IMapRegion region, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
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

            void HandleChunkLoading(IMapChunk mapChunk, int chunkX, int chunkZ)
            {
                if (ringMapSize == -500) { return; }
                int regionX = chunkX / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
                int regionZ = chunkZ / (sapi.WorldManager.RegionSize / sapi.WorldManager.ChunkSize);
                //HandleChunkLoading would perform the same math... so we just figure out what region the chunk is in and call HandleRegionLoading!
                HandleRegionLoading(null, regionX, regionZ);
            }

            //Add our method to the MapRegionGeneration event, causing it to be called any time the engine wants to generate a new region
            MapRegionGeneratorDelegate regionHandler = HandleRegionLoading;
            sapi.Event.MapRegionGeneration(regionHandler, "standard");

            //Add the chunk method to MapChunkGeneration; this is triggered any time a new chunk column is requested.
            MapChunkGeneratorDelegate chunkHandler = HandleChunkLoading;
            sapi.Event.MapChunkGeneration(chunkHandler, "standard");
        }

        //RandomDoubleInRange does what it says, giving a random double between minVal and maxVal.
        // TODO: maybe add distributions to weight the results?
        // DONE: Can now supply RandomizeParams with a distribution type to mess with how it chooses values.
        // I can probably eliminate this function entirely at some point but I can't be assed.
        public double RandomDoubleInRange(ICoreServerAPI api, double minVal, double maxVal)
        {
            return sapi.World.Rand.NextDouble() * (maxVal - minVal) + minVal;
        }

        //RandomizeParams takes WorldgenParamsToScramble and loops through every world generator parameter, creating a dictionary
        // of randomized values by calling RandomDoubleInRange. The dictionary is passed out via "out" params. The seed is also randomized.
        public void RandomizeParams(ICoreServerAPI api, out Dictionary<string, double> newParams, out int newSeed, EnumDistribution dist = EnumDistribution.UNIFORM)
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
            // TODO: Remove all WriteLines when worldgen is complete
            //Debug.WriteLine("New Seed:" + newSeed);
        }

        //SetWorldParams takes the parameters and seed provided and updates the world generator with them.
        public void SetWorldParams(ICoreServerAPI api, Dictionary<string, double> keyValuePairs, int seed)
        {
            foreach (var item in keyValuePairs)
            {
                sapi.World.Config.SetDouble(item.Key, item.Value);
                sapi.World.Config.TryGetAttribute(item.Key, out IAttribute curAttr);
                //Debug.WriteLine(item.Key + "=" + curAttr);
            }
            sapi.WorldManager.SaveGame.Seed = seed;
        }

        //Turns off chunk generation and sending to clients, reloads all of the worldgen parameters (seed, multipliers), and then re-enables everything.
        // Necessary any time we change what ring we're generating.
        public void RestartChunkGenerator()
        {
            // This was built using the logic for /wgen regen as a template. The command paused the chunkdbthread before doing any of this type of stuff,
            // but this mod seems to function fine without doing that... hopefully it wasn't important :^)

            sapi.WorldManager.AutoGenerateChunks = false;
            sapi.WorldManager.SendChunks = false;
            sapi.Assets.Reload(new AssetLocation("worldgen/"));
            var patchLoader = sapi.ModLoader.GetModSystem<ModJsonPatchLoader>();
            patchLoader.ApplyPatches("worldgen/");
            sapi.Event.TriggerInitWorldGen();
            sapi.WorldManager.AutoGenerateChunks = true;
            sapi.WorldManager.SendChunks = true;
        }

        void RegisterChatCommands()
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
        }
    }
}

