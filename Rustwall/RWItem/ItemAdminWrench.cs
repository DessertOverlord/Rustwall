using Microsoft.Win32.SafeHandles;
using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Rustwall.RWItem
{
    internal class ItemAdminWrench : Item
    {
        ICoreServerAPI sapi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            sapi = api as ICoreServerAPI;
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IWorldAccessor world = byEntity.World;
            BlockEntityRebuildable ber = world.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(blockSel.Position);
            IPlayer byplayer = (byEntity as EntityPlayer)?.Player;

            if (ber is not null && byplayer is not null && world.Side == EnumAppSide.Server)
            {
                ber.ownBehavior.DamageOneStage(world, byplayer, ber, blockSel);
            }

            handling = EnumHandHandling.PreventDefault;

            return;
        }
    }
}
