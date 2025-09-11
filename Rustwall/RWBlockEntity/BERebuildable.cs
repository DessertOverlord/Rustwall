using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using Rustwall.ModSystems.RebuildableBlock;
using Rustwall.RWBehaviorRebuildable;
//using Rustwall.RWBlockBehavior;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace Rustwall.RWBlockEntity.BERebuildable
{
    public class BlockEntityRebuildable : BlockEntity
    {
        public int maxStage { get; private set; }
        public int rebuildStage;
        public int itemsUsedThisStage;
        public bool repairLock;
        public bool isFullyRepaired { get { return rebuildStage >= maxStage; } }
        public BehaviorRebuildable ownBehavior;

        private string curRebID = "";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBehavior = Block.BlockBehaviors.ToList().Find(x => x.GetType() == typeof(BehaviorRebuildable)) as BehaviorRebuildable;
            maxStage = ownBehavior.numStages;

            if (Block.Variant["repairstate"] == "repaired") { rebuildStage = maxStage; }

            string rebuildableID = "";

            foreach (var (x, y) in ownBehavior.itemPerStage.Zip(ownBehavior.quantityPerStage))
            {
                rebuildableID += x.ToString() + y.ToString();
            }

            if (curRebID != rebuildableID && curRebID != "")
            {
                rebuildStage = maxStage;
                itemsUsedThisStage = 0;
                if (!ownBehavior.canRepairBeforeBroken)
                {
                    repairLock = true;
                }

                //DoFullRepair does not require slot, BlockSel, or ByPlayer for any functionality (I'm too lazy to reorder the args)
                // I just removed them instead :]
                ownBehavior.DoFullRepair(api.World, this);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("rebuildStage", rebuildStage);
            tree.SetInt("itemsUsedThisStage", itemsUsedThisStage);
            tree.SetBool("repairLock", repairLock);

            string rebuildableID = "";
            foreach (var (x, y) in ownBehavior.itemPerStage.Zip(ownBehavior.quantityPerStage))
            {
                rebuildableID += x.ToString() + y.ToString();
            }

            tree.SetString("rebuildableItemsHash", rebuildableID);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            rebuildStage = tree.GetAsInt("rebuildStage");
            itemsUsedThisStage = tree.GetAsInt("itemsUsedThisStage");
            repairLock = tree.GetAsBool("repairLock") || false;
            curRebID = tree.GetString("rebuildableItemsHash");
        }
    }
}
