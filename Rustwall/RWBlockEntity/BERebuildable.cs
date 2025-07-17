using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        BehaviorRebuildable ownBehavior;
        public bool contributing = false;
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBehavior = Block.BlockBehaviors.ToList().Find(x => x.GetType() == typeof(BehaviorRebuildable)) as BehaviorRebuildable;
            maxStage = ownBehavior.numStages;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("rebuildStage", rebuildStage);
            tree.SetInt("itemsUsedThisStage", itemsUsedThisStage);
            tree.SetBool("repairLock", repairLock);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            //base.FromTreeAttributes(tree, worldAccessForResolve);

            rebuildStage = tree.GetAsInt("rebuildStage");
            itemsUsedThisStage = tree.GetAsInt("itemsUsedThisStage");
            repairLock = tree.GetAsBool("repairLock") || false;
        }
    }
}
