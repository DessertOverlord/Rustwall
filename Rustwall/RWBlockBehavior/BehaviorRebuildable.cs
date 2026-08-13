using Rustwall.Configs;
using Rustwall.ModSystems;
using Rustwall.RWBlockEntity.BERebuildable;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using static Rustwall.RWBlockEntity.BERebuildable.BERebuildable;

namespace Rustwall.RWBehaviorRebuildable
{
    public class BehaviorRebuildable : BlockBehavior
    {
        public int numStages = 0;
        public List<string> itemPerStage { get; private set; } = new List<string>();
        public List<int> quantityPerStage { get; private set; } = new List<int>();

        private static List<ItemStack> allWrenchItemStacks = new List<ItemStack>();

        //public RustwallConfig config;

        public BehaviorRebuildable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
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
            BERebuildable be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BERebuildable;

            IServerPlayer serverPlayer = world.Side == EnumAppSide.Server ? (byPlayer as IServerPlayer) : null;

            //there is no case where the block should do something when a player's hand is empty
            if (slot.Empty || slot.Itemstack == null || be == null)
            {
                serverPlayer?.SendIngameError("rustwall:interact-emptyhand");
                return true;
            }

            if (be.CurentRebuildStage == be.MaxStage)
            {
                serverPlayer?.SendIngameError("rustwall:interact-fullyrepaired");
                return true;
            }

            if (be.RepairLock)
            {
                serverPlayer?.SendIngameError("rustwall:interact-repairlock");
                return true;
            }

            //if the block is not able to be partially repaired, this resets the repair lock on the block on the next interaction once it breaks fully
            if (be.RepairLock && be.CurentRebuildStage == 0 && be.ItemsUsedThisStage == 0)
            {
                be.RepairLock = false;
            }

            //checks if the block needs to be repaired or is repair locked
            if (be.CurentRebuildStage < numStages && !be.RepairLock)
            {
                AssetLocation assetThisStage = new AssetLocation(itemPerStage[be.CurentRebuildStage]);

                if (assetThisStage.Path == "wrench-*")
                {
                    if (allWrenchItemStacks.Count <= 0)
                    {
                        Item[] wrenches = world.SearchItems(assetThisStage);
                        foreach (var item in wrenches) { allWrenchItemStacks.Add(new ItemStack(item)); }
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
                        slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack) >= quantityPerStage[be.CurentRebuildStage]
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
                            slot.Itemstack.Item.DamageItem(world, byPlayer.Entity, slot, quantityPerStage[be.CurentRebuildStage]);
                        }

                        bool result = be.RepairByOneStage(world, slot, blockSel, byPlayer);

                        return result;
                    }
                    //otherwise, subtract just one item
                    else
                    {
                        return be.RepairByOneItem(world, slot, blockSel, byPlayer);
                    }
                }
                else
                {
                    if (slot.Itemstack.Collectible.Code.PathStartsWith("wrench") && slot.Itemstack.Item.GetRemainingDurability(slot.Itemstack) < quantityPerStage[be.CurentRebuildStage])
                    {
                        serverPlayer?.SendIngameError("rustwall:interact-notenoughdurability");
                    }
                    else
                    {
                        serverPlayer?.SendIngameError("rustwall:interact-wrongitem");
                    }
                }
            }


            /// Debug section
            if (slot.Itemstack.Collectible.Code == "game:peatbrick" && serverPlayer is not null)
            {
                if (be is null)
                {
                    Debug.WriteLine("Undefined");
                }
                else
                {
                    string outputText = "";
                    if (be.CurentRebuildStage == be.MaxStage)
                    {
                        outputText = "Operational";
                    }
                    else if (be.CurentRebuildStage > 0)
                    {
                        if (be.RepairLock)
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

                    int curStability = be.CurStability;

                    //if (world?.Api.Side == EnumAppSide.Client && (world?.Api as ICoreClientAPI)?.Settings.Bool.Get("extendedDebugInfo") == true)
                    {
                        string machineType = be.RebuildableBlockType == EnumRebuildableBlockType.Simple ? "Simple" : "Complex";
                        string graceperiod = be.IsGracePeriodActive ? (be.GracePeriodExpirationDate - (world.Api as ICoreServerAPI).World.Calendar.ElapsedDays).ToString("#.##") + " days" : "Inactive";

                        outputText += ("\nType: " + machineType + "\nRebuild Stage: " + be.CurentRebuildStage + "\nMax Rebuild Stage: " + be.MaxStage + "\nItems Used This Stage: " + be.ItemsUsedThisStage + "\nRepair Lock: " + be.RepairLock + "\nGrace Period: " + graceperiod);

                        outputText += ("\nCurrent Global Stability Contribution: " + curStability + "\nMax Global Stability Contribution: " + be.MaxStability);
                    }

                    Debug.WriteLine(outputText);
                }

            }

            return true;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BERebuildable be = world.BlockAccessor.GetBlockEntity(pos) as BERebuildable;

            if (be is null)
            {
                return "Undefined";
            }
            else 
            {
                string outputText = "";
                if (be.CurentRebuildStage == be.MaxStage)
                {
                    outputText = "Operational";
                }
                else if (be.CurentRebuildStage > 0)
                {
                    if (be.RepairLock)
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

                int curStability = be.CurStability;

                if (world?.Api.Side == EnumAppSide.Client && (world?.Api as ICoreClientAPI)?.Settings.Bool.Get("extendedDebugInfo") == true)
                {
                    string machineType = be.RebuildableBlockType == EnumRebuildableBlockType.Simple ? "Simple" : "Complex";
                    string graceperiod = be.IsGracePeriodActive ? (be.GracePeriodExpirationDate - (world.Api as ICoreClientAPI).World.Calendar.ElapsedDays).ToString("#.##") + " days" : "Inactive";

                    outputText += ("\nType: " + machineType + "\nRebuild Stage: " + be.CurentRebuildStage + "\nMax Rebuild Stage: " + be.MaxStage + "\nItems Used This Stage: " + be.ItemsUsedThisStage + "\nRepair Lock: " + be.RepairLock + "\nGrace Period: " + graceperiod);

                    outputText += ("\nCurrent Global Stability Contribution: " + curStability + "\nMax Global Stability Contribution: " + be.MaxStability);
                }

                return outputText; 
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            var be = selection.Block.GetBlockEntity<BERebuildable>(selection);
            if (be == null || be.CurentRebuildStage == be.MaxStage || be.RepairLock == true)
            {
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);
            }

            int index = be.CurentRebuildStage;
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
                    quantityThisStage - be.ItemsUsedThisStage
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
    }
}
