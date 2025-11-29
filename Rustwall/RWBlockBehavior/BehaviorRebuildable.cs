using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWEntityBehavior;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.API.Config;
using System.Diagnostics;

namespace Rustwall.RWBehaviorRebuildable
{
    public class BehaviorRebuildable : BlockBehavior
    {
        public int numStages = 0;
        public bool canRepairBeforeBroken;
        public List<string> itemPerStage { get; private set; } = new List<string>();
        public List<int> quantityPerStage { get; private set; } = new List<int>();

        private static List<ItemStack> allWrenchItemStacks = [];

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
            //handling = EnumHandling.PreventSubsequent;
            handling = EnumHandling.Handled;
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            BlockEntityRebuildable be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRebuildable;

            IServerPlayer serverPlayer = world.Side == EnumAppSide.Server ? (byPlayer as IServerPlayer) : null;

            if (be.repairLock)
            {
                serverPlayer?.SendIngameError("rustwall:interact-repairlock");
                return true;
            }

            if (be.rebuildStage == be.maxStage)
            {
                serverPlayer?.SendIngameError("rustwall:interact-fullyrepaired");
                return true;
            }

            //there is no case where the block should do something when a player's hand is empty
            if (slot.Empty || slot.Itemstack == null || be == null)
            {
                serverPlayer?.SendIngameError("rustwall:interact-emptyhand");
                return true;
            }

            //if the block is not able to be partially repaired, this resets the repair lock on the block on the next interaction
            if (be.repairLock && be.rebuildStage == 0 && be.itemsUsedThisStage == 0) 
            {
                be.repairLock = false;
            }

            //checks if the block needs to be repaired or is repair locked
            if (be.rebuildStage < numStages && !be.repairLock)
            {
                AssetLocation assetThisStage = new AssetLocation(itemPerStage[be.rebuildStage]);

                if (assetThisStage.Path == "wrench-*")
                {
                    if (allWrenchItemStacks.Count <= 0)
                    {
                        Item[] wrenches = world.SearchItems(assetThisStage);
                        foreach (var item in wrenches) { allWrenchItemStacks.Add(new ItemStack(item));}
                    }
                }

                if (
                    (
                    slot.Itemstack?.Collectible.Code.Path == assetThisStage.Path
                    )
                    ||
                    (
                        assetThisStage.Path == "wrench-*" &&
                        allWrenchItemStacks.Any(x => (x.Id == slot.Itemstack.Id)) &&
                        slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack) >= quantityPerStage[be.rebuildStage]
                    )
                    ||
                    (
                        slot.Itemstack?.Collectible.Code.Path == "wrench-admin"
                    )
                )
                {
                    //if the item is a wrench, repair by a whole stage and subtract durability
                    if (slot.Itemstack.Collectible.Code.PathStartsWith("wrench"))
                    {
                        // If we aren't using the admin wrench, subtract durability
                        if (!(slot.Itemstack?.Collectible.Code.Path == "wrench-admin"))
                        {
                            slot.Itemstack.Item.DamageItem(world, byPlayer.Entity, slot, quantityPerStage[be.rebuildStage]);
                        }

                        return RepairByOneStage(world, slot, be, blockSel, byPlayer);
                    }
                    //otherwise, subtract just one item
                    else
                    {
                        return RepairByOneItem(world, slot, be, blockSel, byPlayer);
                    }
                } 
                else
                {
                    if (slot.Itemstack.Collectible.Code.PathStartsWith("wrench") && slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack) < quantityPerStage[be.rebuildStage])
                    {
                        serverPlayer?.SendIngameError("rustwall:interact-notenoughdurability");
                    }
                    else
                    {
                        serverPlayer?.SendIngameError("rustwall:interact-wrongitem");
                    }
                }
            }

            return true;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            //string text = base.GetPlacedBlockInfo(world, pos, forPlayer).Trim();

            BlockEntityRebuildable be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityRebuildable;

            if (be is null)
            {
                return "Undefined";
            }
            else 
            {
                //Debugging
                //return text + "rebuildStage: " + be.rebuildStage + "\nitemsUsedThisStage: " + be.itemsUsedThisStage + "\nRepair Lock: " + be.repairLock;
                string outputText = "";
                if (be.rebuildStage == be.maxStage)
                {
                    outputText = "Operational";
                }
                else if (be.rebuildStage > 0)
                {
                    if (be.repairLock)
                    {
                        outputText = "Operational";
                    }
                    else
                    {
                        outputText = "Damaged";
                    }
                }
                else
                {
                    outputText = "Broken";
                }

                return outputText; 
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            var be = selection.Block.GetBlockEntity<BlockEntityRebuildable>(selection);
            if (be == null || be.rebuildStage == be.maxStage || be.repairLock == true)
            {
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);
            }

            int index = be.rebuildStage;
            AssetLocation assetThisStage = new AssetLocation(itemPerStage[index]);
            int quantityThisStage = assetThisStage.Path.StartsWith("wrench") ? 1 : quantityPerStage[index];
            
            ItemStack[] itemStackThisStage = [];

            if (assetThisStage.Path == "wrench-*") 
            { 
                if (allWrenchItemStacks.Count <= 0)
                {
                    Item[] wrenches = world.SearchItems(assetThisStage);
                    foreach (var item in wrenches) { allWrenchItemStacks.Add(new ItemStack(item)); }
                }

                itemStackThisStage = allWrenchItemStacks.ToArray();
            }
            else
            {
                itemStackThisStage = [new ItemStack
                    (
                    world.GetItem(assetThisStage) != null ? world.GetItem(assetThisStage) : world.GetBlock(assetThisStage),
                    quantityThisStage - be.itemsUsedThisStage
                    )];
            }

            WorldInteraction[] interaction = [new WorldInteraction
            {
                MouseButton = EnumMouseButton.Right,
                ActionLangCode = "rustwall:blockhelp-rebuildable",
                Itemstacks = itemStackThisStage
            }];

            return interaction;
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

        public bool DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockEntityRebuildable be, BlockSelection blockSel)
        {
            if (be.rebuildStage <= 0) { return false; }

            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), be.Pos, -0.25, null, true, 16);
            be.MarkDirty(true);

            if (be.isFullyRepaired)
            {
                int newBlockID = world.GetBlock(block.CodeWithVariant("repairstate", "broken")).Id;
                world.BlockAccessor.ExchangeBlock(newBlockID, be.Pos);
            }
            be.rebuildStage--;
            be.itemsUsedThisStage = 0;

            //We want to remove a contributor only if it is fully destroyed.
            if (be.rebuildStage == 0)
            {
                var beb = be.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                if (beb != null)
                {
                    beb.RemoveContributor();
                }

                be.DeactivateAnimations();

                be.repairLock = false;
            }

            return true;
        }

        private bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
        {
            slot.TakeOut(1);
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            slot.MarkDirty();
            be.itemsUsedThisStage++;

            be.MarkDirty(true);

            if (be.itemsUsedThisStage >= quantityPerStage[be.rebuildStage])
            {
                be.rebuildStage++;
                be.itemsUsedThisStage = 0;
                be.MarkDirty(true);
            }

            if (be.rebuildStage >= numStages) { DoFullRepair(world, be); }

            return true;
        }

        private bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

            slot.MarkDirty();
            be.itemsUsedThisStage = 0;
            be.rebuildStage++;

            be.MarkDirty(true);

            if (be.rebuildStage >= be.maxStage)
            {
                DoFullRepair(world, be);
            }

            return true;
        }

        public void DoFullRepair(IWorldAccessor world, BlockEntityRebuildable be)
        {
            int newBlockID = world.GetBlock(block.CodeWithVariant("repairstate", "repaired")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, be.Pos);

            var beb = be.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
            if (beb != null)
            {
                beb.AddContributor();
            }

            be.repairLock = true;
            be.ActivateAnimations();
        }
    }
}
