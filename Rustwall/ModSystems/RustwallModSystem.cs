using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Diagnostics;
using Rustwall.Configs;
using System;

namespace Rustwall.ModSystems
{
    public abstract class RustwallModSystem : ModSystem
    {
        protected ICoreServerAPI sapi;
        public RustwallConfig config;
        protected string baseDir = "rustwall/";
        string configName = "rustwall.json";
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            LoadConfig();
            Debug.WriteLine("Rustwall base modsystem init server side. Hello world!");
            RustwallStartServerSide();
        }

        public override void Start(ICoreAPI api)
        {
            Debug.WriteLine("Rustwall base modsystem init. Hello world!");
        }

        protected abstract void RustwallStartServerSide();

        private void LoadConfig()
        {
            //sapi.GetOrCreateDataPath("ModConfig/Rustwall");
            Debug.WriteLine("LoadConfig hit");
            try
            {
                config = sapi.LoadModConfig<RustwallConfig>(baseDir + configName);
            }
            catch (Exception)
            {
                sapi.Server.LogError("Exception loading Rustwall config");
            }

            if (config == null)
            {
                sapi.Server.LogError("Rustwall config not loaded correctly, initializing default.");

                config = new RustwallConfig();
                sapi.StoreModConfig(config, baseDir + configName);
            }
        }
    }
}
