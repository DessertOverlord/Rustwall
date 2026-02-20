using Microsoft.Win32.SafeHandles;
using Rustwall.ModSystems.RebuildableBlock;
using Rustwall.RWBehaviorRebuildable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
//using Rustwall.RWBlockBehavior;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace Rustwall.RWBlockEntity.BERebuildable
{
    public class BlockEntityRebuildable : BlockEntity
    {
        /// <summary>
        /// Maximum number of rebuild stages
        /// </summary>
        public int maxStage { get; private set; }
        /// <summary>
        /// Current rebuild stage
        /// </summary>
        public int rebuildStage;
        /// <summary>
        /// Number of items used to repair; typically items work towards completing a stage
        /// </summary>
        public int itemsUsedThisStage;
        /// <summary>
        /// Whether or not repairs are disabled; used for complex machines
        /// </summary>
        public bool repairLock;
        /// <summary>
        /// Simple bool for whether or not the machine is fully repaired.
        /// </summary>
        public bool isFullyRepaired { get { return rebuildStage >= maxStage; } }
        /// <summary>
        /// Easy way to access this BE's own behavior
        /// </summary>
        public BehaviorRebuildable ownBehavior;
        /// <summary>
        /// Duration in game calendar days of the current repair grace period
        /// </summary>
        public double gracePeriodDuration = 0;
        /// <summary>
        /// Simple bool for whether or not the grace period is currently active
        /// </summary>
        public bool isGracePeriodActive { get { return gracePeriodDuration > 0; } }
        /// <summary>
        /// String for the rebuildable block ID / hash code, used to determine if the
        /// items needed to repair a block have changed.
        /// </summary>
        private string curRebID = "";
        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        } 

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api as ICoreServerAPI == null) animUtil?.InitializeAnimator("rebuildableblock");

            ownBehavior = Block.BlockBehaviors.ToList().Find(x => x.GetType() == typeof(BehaviorRebuildable)) as BehaviorRebuildable;
            maxStage = ownBehavior.numStages;

            if (Block.Variant["repairstate"] == "repaired") { rebuildStage = maxStage; ActivateAnimations(); if (!ownBehavior.canRepairBeforeBroken) { repairLock = true; } }

            string rebuildableID = "";

            // Takes all of the items and quantities needed to rebuild a given block and makes what is essentially a hash code for it
            foreach (var (x, y) in ownBehavior.itemPerStage.Zip(ownBehavior.quantityPerStage))
            {
                rebuildableID += x.ToString() + y.ToString();
            }

            //determine if the hash above differs from what we had stored previously (the costs for rebuilding have been updated)
            if (curRebID != rebuildableID && curRebID != "")
            {
                // if they differ, repair all blocks affected fully to prevent weird undefined behavior
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

        public void ActivateAnimations()
        {
            animUtil?.StartAnimation(new AnimationMetaData() { Animation = "active", Code = "active", EaseInSpeed = 1, EaseOutSpeed = 1, AnimationSpeed = 1f });
            MarkDirty(true);
        }

        public void DeactivateAnimations()
        {
            animUtil?.StopAnimation("active");
            MarkDirty(true);
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
