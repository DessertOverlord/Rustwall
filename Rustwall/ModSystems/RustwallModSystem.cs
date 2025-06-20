using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Diagnostics;

namespace Rustwall.ModSystems
{
    public class RustwallModSystem : ModSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            RustwallStartServerSide(api);
        }

        protected virtual void RustwallStartServerSide(ICoreServerAPI api)
        {
            Debug.WriteLine("Rustwall base modsystem init. Hello world!");
        }
    }
}
