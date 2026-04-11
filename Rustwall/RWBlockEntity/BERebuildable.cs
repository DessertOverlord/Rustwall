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
    public abstract class BlockEntityRebuildable : BlockEntity
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
        /// Simple bool for whether or not the grace period is currently active.
        /// Uses null checks to avoid crashes from server-side or client-side access.
        /// </summary>
        public bool isGracePeriodActive
        {
            get
            {
                if (sapi is not null)
                {
                    return gracePeriodExpirationDate > sapi.World.Calendar.ElapsedDays;
                }
                else
                {
                    return gracePeriodExpirationDate > capi.World.Calendar.ElapsedDays;
                }
            }
        }
        /// <summary>
        /// Date in calendar days when the grace period will expire. Easier to calculate with.
        /// </summary>
        public double gracePeriodExpirationDate { get; set; }
        public GlobalStabilitySystem globalStabSys;
        public int curStability { get; private set; } 
        public int maxStability { get; private set; } 

        public ICoreServerAPI sapi;
        public ICoreClientAPI capi;
        /// <summary>
        /// String for the rebuildable block ID / hash code, used to determine if the
        /// items needed to repair a block have changed.
        /// </summary>
        private string curRebID = "";
        public abstract EnumRebuildableBlockType rebuildableBlockType { get; }
        public virtual bool canRepairBeforeBroken { 
            get 
            {
                if (rebuildableBlockType == EnumRebuildableBlockType.Simple)
                {
                    return true;
                }
                else return false;
            } 
        
        }

        public enum EnumRebuildableBlockType
        {
            Simple,
            Complex
        }


        
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
            else if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }

            //Must be done client-side
            if (animatible)
            {
                InitAnimations(api);
                if (Block.Variant["repairstate"] == "repaired")
                {
                    ActivateAnimations();
                }
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
                RepairFully(api.World);
            }

            //Global Stability section

            if (api.Side == EnumAppSide.Server)
            {
                globalStabSys = (api as ICoreServerAPI).ModLoader.GetModSystem<GlobalStabilitySystem>();
                globalStabSys.allStableBlockEntities.Add(Pos);

                if (Block.Variant["repairstate"] == "repaired")
                {
                    rebuildStage = maxStage;
                    if (!canRepairBeforeBroken) { repairLock = true; }
                    AddContributor();
                }
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

        //Because the GlobalStabilitySystem calls ActivateAnimations and DeactivateAnimations on the server side,
        //we need to make sure the client does the same because animUtil is not defined server-side.
        //We use network packets to achieve this as it syncs with all players.
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == 1337)
            {
                string strData = "";
                try
                {
                    strData = SerializerUtil.Deserialize<string>(data);
                }
                catch (Exception e)
                { 
                    
                }
                
                if (strData == "activate")
                {
                    ActivateAnimations();
                }
                else if (strData == "deactivate")
                {
                    DeactivateAnimations();
                }
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

        protected void RemoveContributor()
        { 
            curStability = 0;
            if (globalStabSys is not null)
            {
                globalStabSys.stabilityContributors.Remove(Pos);
            }
            MarkDirty(true);
        }

        protected void AddContributor()
        {
            curStability = maxStability;
            if (globalStabSys is not null)
            {
                globalStabSys.stabilityContributors.Add(Pos);
            }
            MarkDirty(true);
        }

        //Repair / Damage Functions

        public abstract void DamageFully(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel);

        public abstract bool DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel);

        public abstract bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer);

        public abstract bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer);

        public abstract void RepairFully(IWorldAccessor world);

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
