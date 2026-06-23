using HarmonyLib;
using Rustwall.Configs;
using System;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Rustwall.ModSystems
{
    public abstract class RustwallModSystem : ModSystem
    {
        protected ICoreServerAPI sapi;
        public static RustwallConfig config;
        private readonly string configName = "rustwall.json";
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            LoadConfig();
            RustwallStartServerSide();
            //loads ALL harmony patches
            var harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();
        }

        protected abstract void RustwallStartServerSide();

        protected void LoadConfig()
        {
            try
            {
                config = sapi.LoadModConfig<RustwallConfig>(configName);
            }
            catch (Exception)
            {
                sapi.Server.LogError("Exception loading Rustwall config at " + configName);
            }

            if (config == null)
            {
                sapi.Server.LogError("Rustwall config not loaded correctly, initializing default.");

                config = new RustwallConfig();
                sapi.StoreModConfig(config, configName);
            }
        }
    }
}
