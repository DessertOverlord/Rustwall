using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
//using Rustwall.Block;
using Rustwall.ModSystems.RebuildableBlock;
using Microsoft.Win32.SafeHandles;
using Rustwall.RWBlockEntity.BETestRebuildable;
using System.Diagnostics;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Rustwall.RWBehaviorRebuildable
{
    internal class BehaviorRebuildable : BlockBehavior
    {
        public int numStages;
        List<string> itemPerStage = new List<string>();
        List<int> quantityPerStage = new List<int>();

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
            if (slot.Empty) return false;
            BlockEntityRebuildable be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRebuildable;
            //BlockEntityRebuildable be = blockSel.Block?.GetBlockEntity<BlockEntityRebuildable>(blockSel);
            if (be != null && be.rebuildStage < numStages)
            {
                if (slot.Itemstack.Collectible.Code.Path == itemPerStage[be.rebuildStage])
                {
                    if (slot.Itemstack.Collectible.Code.PathStartsWith("wrench"))
                    {
                        world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
                        slot.Itemstack.Item.DamageItem(world, byPlayer as Entity, slot, quantityPerStage[be.rebuildStage]);
                        slot.MarkDirty();
                        be.itemsUsedThisStage = 0;
                        be.rebuildStage++;
                        Debug.WriteLine("BE Rebuild state is currently: " + be.rebuildStage);
                        be.MarkDirty(true);
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

                    Debug.WriteLine("BE Rebuild state is currently: " + be.rebuildStage);
                }
            }

            return true;//base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }


    }
}
