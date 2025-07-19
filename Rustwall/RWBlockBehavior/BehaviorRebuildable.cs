using System.Collections.Generic;
using Vintagestory.API.Common;
using Rustwall.RWBlockEntity.BERebuildable;
using System.Diagnostics;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata.Ecma335;
using Vintagestory.GameContent;
using Vintagestory.API.Server;


namespace Rustwall.RWBehaviorRebuildable
{
    internal class BehaviorRebuildable : BlockBehavior
    {
        public int numStages = 0;
        public bool canRepairBeforeBroken;
        List<string> itemPerStage = new List<string>();
        List<int> quantityPerStage = new List<int>();
        

        public BehaviorRebuildable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            canRepairBeforeBroken = properties["canRepairBeforeBroken"].AsBool();

            var stages = properties["stages"].AsArray();
            
            //loop through all of the available stages
            foreach (var item in stages)
            {
                itemPerStage.Add(item["item"].AsString());
                quantityPerStage.Add(item["quantity"].AsInt());
                numStages++;
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventSubsequent;
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            BlockEntityRebuildable be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRebuildable;

            //there is no case where the block should do something when a player's hand is empty
            if (slot.Empty || slot.Itemstack == null || be == null) return false;

            //if the block is not able to be partially repaired, this resets the repair lock on the block on the next interaction
            if (be.repairLock && be.rebuildStage == 0 && be.itemsUsedThisStage == 0) { be.repairLock = false; }

            //checks if the block needs to be repaired or is repair locked
            if (be.rebuildStage < numStages && !be.repairLock)
            {
                //TODO: add flexibility for item checking -- all kinds of wrenches, for instance
                if (slot.Itemstack?.Collectible.Code.Path == itemPerStage[be.rebuildStage])
                {
                    //if the item is a wrench, repair by a whole stage and subtract durability
                    if (slot.Itemstack.Collectible.Code.PathStartsWith("wrench"))
                    {
                        return RepairByOneStage(world, slot, be, blockSel, byPlayer);
                    }
                    //otherwise, subtract just one item
                    else
                    {
                        return RepairByOneItem(world, slot, be, blockSel, byPlayer);
                    }
                }
            }

            //allows rusty gears to be used to damage blocks for testing
            //REMOVE IN LIVE!
            if (slot.Itemstack.Collectible.Code.Path == "gear-rusty" && be.rebuildStage != 0)
            {
                //DoBreakFully(world, byPlayer, be, blockSel);
                DamageOneStage(world, byPlayer, be, blockSel);
            }


            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }
        public void DoBreakFully(IWorldAccessor world, IPlayer byPlayer, BlockEntityRebuildable be, BlockSelection blockSel)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

            if (be.isFullyRepaired)
            {
                Block newBlock = world.GetBlock(block.CodeWithVariant("repairstate", "broken"));
                world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);

                var newBE = world.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(blockSel.Position);

                if (newBE != null)
                {
                    newBE.rebuildStage = 0;
                    newBE.itemsUsedThisStage = 0;
                    newBE.repairLock = be.repairLock;
                }
            }

            be.rebuildStage = 0;
            be.itemsUsedThisStage = 0;
        }

        public void DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockEntityRebuildable be, BlockSelection blockSel)
        {

            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

            if (be.isFullyRepaired)
            {
                Block newBlock = world.GetBlock(block.CodeWithVariant("repairstate", "broken"));
                world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);

                var newBE = world.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(blockSel.Position);
                
                if (newBE != null)
                {
                    newBE.rebuildStage = be.rebuildStage - 1;
                    newBE.itemsUsedThisStage = 0;
                    newBE.repairLock = be.repairLock;
                }
            }

            be.rebuildStage--;
            be.itemsUsedThisStage = 0;
        }

        private bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
        {
            slot.TakeOut(1);
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            slot.MarkDirty();
            be.itemsUsedThisStage++;

            if (be.itemsUsedThisStage >= quantityPerStage[be.rebuildStage])
            {
                be.rebuildStage++;
                be.itemsUsedThisStage = 0;
                be.MarkDirty(true);
            }

            if (be.rebuildStage >= numStages) { DoFullRepair(world, slot, be, blockSel, byPlayer); }

            return true;
        }

        private bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
        {
            if (slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack) < quantityPerStage[be.rebuildStage]) { return false; }

            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

            slot.Itemstack.Item.DamageItem(world, byPlayer.Entity, slot, quantityPerStage[be.rebuildStage]);

            slot.MarkDirty();
            be.itemsUsedThisStage = 0;
            be.rebuildStage++;

            be.MarkDirty(true);

            if (be.rebuildStage >= be.maxStage)
            {
                DoFullRepair(world, slot, be, blockSel, byPlayer);
            }

            return true;
        }

        public void DoFullRepair(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
        {
            //We need to initialize a new block that is the repaired version of the old block
            Block newBlock = world.GetBlock(block.CodeWithVariant("repairstate", "repaired"));
            world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);

            //when we do this, the block entity of the new block will be different from the old one, so we want to acquire it and...
            var newBE = world.BlockAccessor.GetBlockEntity<BlockEntityRebuildable>(blockSel.Position);

            if (newBE != null )
            {
                //set the fields of the new block entity to the ones used in the old.
                newBE.rebuildStage = be.rebuildStage;
                if (!canRepairBeforeBroken) { newBE.repairLock = true; }
            }
        }
    }
}
