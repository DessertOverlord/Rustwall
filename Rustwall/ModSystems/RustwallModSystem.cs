using HarmonyLib;
using Rustwall.Configs;
using System;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Rustwall.ModSystems
{
    public abstract class RustwallModSystem : ModSystem
    {
        protected ICoreServerAPI sapi;
        public RustwallConfig config { get; private set; }
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
                sapi.Server.Logger.Error("Exception loading Rustwall config at " + configName);
            }

            if (config == null)
            {
                sapi.Server.Logger.Error("Rustwall config not loaded correctly, initializing default.\nThis is normal on the first load.");

                config = new RustwallConfig();
                sapi.StoreModConfig(config, configName);
            }
        }

        public void ReloadConfig()
        {
            config = null;

            LoadConfig();
        }
    }
}
