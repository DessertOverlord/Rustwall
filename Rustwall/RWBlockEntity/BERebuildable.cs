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

//using Rustwall.RWBlockBehavior;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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
        //public double gracePeriodDuration { get { return gracePeriodExpirationDate - sapi.World.Calendar.ElapsedDays; } }
        /// <summary>
        /// Simple bool for whether or not the grace period is currently active
        /// </summary>
        public bool isGracePeriodActive { get { return gracePeriodExpirationDate > sapi.World.Calendar.ElapsedDays; } }
        /// <summary>
        /// Date in calendar days when the grace period will expire. Easier to calculate with.
        /// </summary>
        public double gracePeriodExpirationDate { get; set; }

        public GlobalStabilitySystem globalStabSys;

        //public BlockEntityRebuildable ber;
        public int curStability { get; private set; } //= 0;
        public int maxStability { get; private set; } //= 0;

        public ICoreServerAPI sapi;
        /// <summary>
        /// String for the rebuildable block ID / hash code, used to determine if the
        /// items needed to repair a block have changed.
        /// </summary>
        private string curRebID = "";
        
        public bool animatible { get { return GetBehavior<BEBehaviorAnimatable>() is not null; } }
        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }

            if (animatible)
            {
                InitAnimations(api);
            }

            ownBehavior = Block.BlockBehaviors.ToList().Find(x => x.GetType() == typeof(BehaviorRebuildable)) as BehaviorRebuildable;
            maxStability = GetBehavior<BEBehaviorGloballyStable>().properties["value"].AsInt();
            maxStage = ownBehavior.numStages;

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

                ///TODO: Replace with DoFullRepair from block behavior. Modularity!
                rebuildStage = maxStage;
                itemsUsedThisStage = 0;
                if (!ownBehavior.canRepairBeforeBroken)
                {
                    repairLock = true;
                }
                
                //DoFullRepair does not require slot, BlockSel, or ByPlayer for any functionality (I'm too lazy to reorder the args)
                // I just removed them instead :]
                DoFullRepair(api.World, this);
            }

            //Global Stability section

            if (api.Side == EnumAppSide.Server)
            {
                globalStabSys = (api as ICoreServerAPI).ModLoader.GetModSystem<GlobalStabilitySystem>();
                globalStabSys.allStableBlockEntities.Add(Pos);

                if (Block.Variant["repairstate"] == "repaired")
                {
                    rebuildStage = maxStage;
                    if (animatible) { ActivateAnimations(); }
                    if (!ownBehavior.canRepairBeforeBroken) { repairLock = true; }
                    AddContributor();
                }

                RegisterGameTickListener(OnServerTick, 5000);
            }
        }

        protected virtual void InitAnimations(ICoreAPI api)
        {
            sapi.Logger.Error("Animatible Rustwall Machine initialized with BlockEntityRebuildable. Move it to its own BlockEntity for animations to work!");
        }
       
        protected virtual void ActivateAnimations()
        {
            sapi.Logger.Error("Animatible Rustwall Machine initialized with BlockEntityRebuildable. Move it to its own BlockEntity for animations to work!");
        }

        protected virtual void DeactivateAnimations()
        {
            sapi.Logger.Error("Animatible Rustwall Machine initialized with BlockEntityRebuildable. Move it to its own BlockEntity for animations to work!");
        }

        //Because the behavior calls ActivateAnimations and DeactivateAnimations on the server,
        //we need to make sure the client does the same because animUtil is not defined server-side.
        //We use network packets to achieve this as it syncs with all players.
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == 1 && animatible)
            {
                bool active = SerializerUtil.Deserialize<bool>(data);

                if (active) { ActivateAnimations(); }
                else { DeactivateAnimations(); }
            }
        }

        public void OnServerTick(float dt)
        {
            //check that this is actually a rebuildable block
            if (this != null)
            {
                ///Check to see if we have a grace period active. 
                ///If we do, decrease the duration and check if it has expired. 
                ///If it has, remove the grace period and update stability accordingly.
                ///

                Debug.WriteLine("dt is " + dt);

                //if the block is already fully repaired and the stability is already set to max, we don't care to check again
                if (rebuildStage == maxStage && curStability == maxStability) { return; }
                /*
                //if the block is rebuilt but our current stability is still zero, correct it and add the block to the list of contributors
                if (ber.rebuildStage == ber.maxStage && curStability == 0)
                {
                    AddContributor();
                    return;
                }
                //if the block is not destroyed and our current stability is not zero, correct it and make sure we're not in the list of contributors
                else if (ber.rebuildStage == 0 && curStability != 0)
                {
                    RemoveContributor();
                    return;
                }*/
            }
            //just in case someone is a doofus
            else
            {
                sapi.Logger.Error("ber was null during OnServerTick... how?");
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                globalStabSys.stabilityContributors.Remove(Pos);
                globalStabSys.allStableBlockEntities.Remove(Pos);
            }
        }

        public void RemoveContributor()
        { 
            curStability = 0;
            globalStabSys.stabilityContributors.Remove(Pos);
            MarkDirty(true);
        }

        public void AddContributor()
        {
            curStability = maxStability;
            if (globalStabSys is not null)
            {
                globalStabSys.stabilityContributors.Add(Pos);
            }
            else
            {
                Debug.WriteLine("globalStabSys is null");
            }
            MarkDirty(true);
        }

        //Repair / Damage Functions

        public void DoBreakFully(IWorldAccessor world, IPlayer byPlayer, BlockEntityRebuildable be, BlockSelection blockSel)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), be.Pos, -0.25, byPlayer, true, 16);

            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "broken")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, be.Pos);

            //var beb = be.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
            if (be != null)
            {
                be.RemoveContributor();
                if (!ownBehavior.canRepairBeforeBroken)
                {
                    be.repairLock = false;
                }

                if (be.animatible)
                {
                    DeactivateAnimations();
                }

                be.MarkDirty(true);
            }

            be.rebuildStage = 0;
            be.itemsUsedThisStage = 0;
        }

        public bool DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockEntityRebuildable be, BlockSelection blockSel)
        {
            if (be.rebuildStage < 0) { return false; }

            if (be.rebuildStage > 0)
            {
                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), be.Pos, -0.25, null, true, 16);

                be.rebuildStage--;
                be.itemsUsedThisStage = 0;

                //We only want to make it appear broken if it is fully broken, not partially damaged.
                //We want to remove a contributor only if it is fully destroyed.
                if (be.rebuildStage == 0)
                {
                    DoBreakFully(world, byPlayer, be, blockSel);
                }

                be.MarkDirty(true);
                return true;
            }

            return false;
        }

        public bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
        {
            slot.TakeOut(1);
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            slot.MarkDirty();
            be.itemsUsedThisStage++;

            be.MarkDirty(true);

            if (be.itemsUsedThisStage >= ownBehavior.quantityPerStage[be.rebuildStage])
            {
                be.rebuildStage++;
                be.itemsUsedThisStage = 0;
                be.MarkDirty(true);
            }

            if (be.rebuildStage >= ownBehavior.numStages) { DoFullRepair(world, be); }

            return true;
        }

        public bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockEntityRebuildable be, BlockSelection blockSel, IPlayer byPlayer)
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
            else
            {
                //be.gracePeriodDuration = config.GracePeriodDurationRepairOneStage;
                be.gracePeriodExpirationDate = world.Calendar.ElapsedDays + BehaviorRebuildable.config.GracePeriodDurationRepairOneStage;
            }

            return true;
        }

        public void DoFullRepair(IWorldAccessor world, BlockEntityRebuildable be)
        {
            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "repaired")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, be.Pos);

            //var beb = be.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
            if (be != null)
            {
                be.AddContributor();
                if (!ownBehavior.canRepairBeforeBroken)
                {
                    be.repairLock = true;
                }
                if (be.animatible)
                {
                    ActivateAnimations();
                }
                be.MarkDirty(true);
            }

            be.gracePeriodExpirationDate = world.Calendar.ElapsedDays + BehaviorRebuildable.config.GracePeriodDurationRepairFully;
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
