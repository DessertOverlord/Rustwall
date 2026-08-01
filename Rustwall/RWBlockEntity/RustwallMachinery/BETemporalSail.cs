using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Rustwall.RWBlockEntity.RustwallMachinery
{
    public class BlockEntityTemporalSail : BlockEntitySimpleRebuildable
    {
        public override void DamageFully(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.DamageFully(world, byPlayer, blockSel);

            if (world.Api.Side == EnumAppSide.Server)
            {
                CreateLightning(world.Api as ICoreServerAPI);
            }
        }

        private void CreateLightning(ICoreServerAPI sapi)
        {
            WeatherSystemServer wsys = (Api as ICoreServerAPI).ModLoader.GetModSystem<WeatherSystemServer>();

            wsys.SpawnLightningFlash(Pos.Copy().ToVec3d());
        }
    }
}
