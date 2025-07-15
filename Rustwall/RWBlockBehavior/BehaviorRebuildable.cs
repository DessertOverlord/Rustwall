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


namespace Rustwall.RWBehaviorRebuildable
{
    internal class BehaviorRebuildable : BlockBehavior
    {
        public int numStages;
        List<string> itemPerStage = new List<string>();
        List<int> quantityPerStage = new List<int>();
        public bool fullyRepaired = false;
        

        public BehaviorRebuildable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            numStages = properties["numStages"].AsInt();

            for (int i = 0; i < numStages; i++) 
            {
                //var thing = properties.AsArray<Dictionary<string, int>>();

                string stageNum = "stage" + i;
                itemPerStage.Add(properties[stageNum]["item"].AsString());
                quantityPerStage.Add(properties[stageNum]["quantity"].AsInt());
            }
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventSubsequent;
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            BlockEntityRebuildable be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRebuildable;
            if (slot.Empty || slot.Itemstack == null || be == null) return false;
            if (be.rebuildStage < numStages)
            {
                if (slot.Itemstack.Collectible.Code.Path == itemPerStage[be.rebuildStage])
                {
                    if (slot.Itemstack.Collectible.Code.PathStartsWith("wrench"))
                    {
                        if (slot.Itemstack.Item.Durability < quantityPerStage[be.rebuildStage]) { return true; }
                        world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
                        slot?.Itemstack?.Item?.DamageItem(world, byPlayer as Entity, slot, quantityPerStage[be.rebuildStage]);
                        slot.MarkDirty();
                        be.itemsUsedThisStage = 0;
                        be.rebuildStage++;
                        Debug.WriteLine("BE Rebuild state is currently: " + be.rebuildStage);
                        be.MarkDirty(true);
                        Block newBlock = world.GetBlock(block.CodeWithVariant("repairstate", "repaired"));

                        world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);
                        return true;
                    }
                    else
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
                            Debug.WriteLine("BE Rebuild state is currently: " + be.rebuildStage);
                            return true;
                        }
                    }
                }
            }
            else if (!fullyRepaired)
            {
                Block newBlock = world.GetBlock(block.CodeWithVariant("repairstate", "repaired"));

                world.BlockAccessor.SetBlock(newBlock.Id, blockSel.Position);

                fullyRepaired = true;

                Debug.WriteLine("BE Rebuild state is currently: " + be.rebuildStage);
            }



            return true;//base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }




    }
}
