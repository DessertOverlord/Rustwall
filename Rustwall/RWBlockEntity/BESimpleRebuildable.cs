using Microsoft.Win32.SafeHandles;
using Rustwall.ModSystems.GlobalStability;
using Rustwall.ModSystems.RebuildableBlock;
using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWEntityBehavior;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using Vintagestory;
using Vintagestory.API.Client;


//using Rustwall.RWBlockBehavior;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;


namespace Rustwall.RWBlockEntity.BERebuildable
{
    public class BlockEntitySimpleRebuildable : BlockEntityRebuildable
    {
        public override EnumRebuildableBlockType rebuildableBlockType { get { return EnumRebuildableBlockType.Simple; } }

        public override bool DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (rebuildStage < 0) { return false; }

            if (rebuildStage > 0)
            {
                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), Pos, -0.25, null, true, 16);

                rebuildStage--;
                itemsUsedThisStage = 0;

                //We only want to make it appear broken if it is fully broken, not partially damaged.
                //We want to remove a contributor only if it is fully destroyed.
                if (rebuildStage == 0)
                {
                    DamageFully(world, byPlayer, blockSel);
                }

                MarkDirty(true);
                return true;
            }

            return false;
        }

        public override void DamageFully(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), Pos, -0.25, byPlayer, true, 16);

            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "broken")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, Pos);

            RemoveContributor();

            if (animatible)
            {
                /// We have to use packet broadcasts so that when the ModSystem (running only server-side) calls to damage the block,
                /// the animation change gets synchronized to the client.
                (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.DeactivateAnimations);
            }

            MarkDirty(true);

            rebuildStage = 0;
            itemsUsedThisStage = 0;
        }

        public override bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            slot.TakeOut(1);
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            slot.MarkDirty();
            itemsUsedThisStage++;

            MarkDirty(true);

            if (itemsUsedThisStage >= ownBehavior.quantityPerStage[rebuildStage])
            {
                RepairByOneStage(world, slot, blockSel, byPlayer);
            }

            return true;
        }

        public override bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

            //world.Api.Logger.Event("RepairByOneStage executed on" + world.Api.Side);

            slot.MarkDirty();
            itemsUsedThisStage = 0;
            rebuildStage++;

            if (rebuildStage == maxStage)
            {
                RepairFully(world);
            }
            else
            {
                gracePeriodExpirationDate = world.Calendar.ElapsedDays + ownBehavior.config.GracePeriodDurationRepairOneStage;
            }

            //Simple machines should contribute and be animated even if they aren't fully repaired.
            if (rebuildStage > 0)
            {
                AddContributor();
                if (animatible)
                {
                    (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.ActivateAnimations);
                }
            }

            MarkDirty(true);
            return true;
        }

        public override void RepairFully(IWorldAccessor world)
        {
            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "repaired")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, Pos);
            AddContributor();
            if (animatible)
            {
                //ActivateAnimations();
                (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.ActivateAnimations);
            }
            MarkDirty(true);

            gracePeriodExpirationDate = world.Calendar.ElapsedDays + ownBehavior.config.GracePeriodDurationRepairFully;
        }
    }
}
