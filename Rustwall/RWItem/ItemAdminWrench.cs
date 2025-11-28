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
    internal class ItemAdminWrench : ItemWrench
    {
        ICoreServerAPI sapi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            sapi = api as ICoreServerAPI;
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            BlockEntityRebuildable ber = sapi?.World.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(blockSel.Position);
            IPlayer byplayer = byEntity as IPlayer;

            if (ber is not null && byplayer is not null)
            {
                ber.ownBehavior.DamageOneStage(sapi.World, byplayer, ber, blockSel);
            }
            else
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
            }
        }
    }
}
