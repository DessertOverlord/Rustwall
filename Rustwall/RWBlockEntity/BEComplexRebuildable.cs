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
    public class BlockEntityComplexRebuildable : BERebuildable
    {
        public override EnumRebuildableBlockType RebuildableBlockType { get { return EnumRebuildableBlockType.Complex; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            if (Block.Variant["repairstate"] == "repaired")
            {
                CurentRebuildStage = MaxStage;
            }

            if (IsFullyRepaired)
            {
                AddContributor(); 
                RepairLock = true;
            }

            if (Animatible)
            {
                InitAnimations(api);
                if (IsFullyRepaired)
                {
                    ActivateAnimations();
                }
            }
        }

        public override bool DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (CurentRebuildStage < 0) { return false; }

            if (CurentRebuildStage > 0)
            {
                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), Pos, -0.25, null, true, 16);

                CurentRebuildStage--;
                ItemsUsedThisStage = 0;

                //We only want to make it appear broken if it is fully broken, not partially damaged.
                //We want to remove a contributor only if it is fully destroyed.
                if (CurentRebuildStage == 0)
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
            RepairLock = false;

            if (Animatible)
            {
                (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.DeactivateAnimations);
            }

            MarkDirty(true);

            CurentRebuildStage = 0;
            ItemsUsedThisStage = 0;
        }

        public override bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            slot.TakeOut(1);
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            slot.MarkDirty();
            ItemsUsedThisStage++;

            MarkDirty(true);

            if (ItemsUsedThisStage >= OwnBehavior.quantityPerStage[CurentRebuildStage])
            {
                RepairByOneStage(world, slot, blockSel, byPlayer);
            }

            return true;
        }

        public override bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            ItemsUsedThisStage = 0;
            CurentRebuildStage++;

            if (CurentRebuildStage == MaxStage)
            {
                RepairFully(world);
            }
            else
            {
                GracePeriodExpirationDate = world.Calendar.ElapsedDays + GracePeriodDurationRepairOneStage;
            }

            MarkDirty(true);

            return true;
        }

        public override void RepairFully(IWorldAccessor world)
        {
            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "repaired")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, Pos);

            AddContributor();
            RepairLock = true;
            if (Animatible)
            {
                (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.ActivateAnimations);
            }
            MarkDirty(true);
                
            GracePeriodExpirationDate = world.Calendar.ElapsedDays + GracePeriodDurationRepairFully;
        }
    }
}
