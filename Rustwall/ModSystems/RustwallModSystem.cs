using HarmonyLib;
using Rustwall.Configs;
using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Rustwall.ModSystems
{
    public abstract class RustwallModSystem : ModSystem
    {
        protected ICoreServerAPI sapi;
        public static RustwallConfig Config { get; private set; }
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
                Config = sapi.LoadModConfig<RustwallConfig>(configName);
            }
            catch (Exception)
            {
                sapi.Server.Logger.Error("Exception loading Rustwall config at " + configName);
            }

            if (Config == null)
            {
                sapi.Server.Logger.Warning("Rustwall config not loaded correctly, initializing default. This is normal on the first load.");

                Config = new RustwallConfig();
                sapi.StoreModConfig(Config, configName);
            }
        }

        public void ReloadConfig()
        {
            Config = null;

            LoadConfig();
        }
    }
}
