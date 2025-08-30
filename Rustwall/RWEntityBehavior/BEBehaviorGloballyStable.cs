using Rustwall.RWBlockEntity.BERebuildable;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Rustwall.ModSystems.GlobalStability;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.GameContent;

namespace Rustwall.RWEntityBehavior
{
    internal class BEBehaviorGloballyStable : BlockEntityBehavior
    {
        public GlobalStabilitySystem modsys { get; set; }
        BlockEntityRebuildable ber;
        public int curStability { get; private set; } = 0;
        public int maxStability { get; private set; } = 0;
        //bool isContributing;
        public BEBehaviorGloballyStable(BlockEntity blockent) : base(blockent)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            maxStability = properties["value"].AsInt();
            //We need to poll the current stability every so often
            //Blockentity.RegisterGameTickListener(QueryAndUpdateCurrentStability, 5000);
            modsys = api.ModLoader.GetModSystem<GlobalStabilitySystem>();
            //will return null if the BlockEntity is not BlockEntityRebuildable!
            ber = Blockentity as BlockEntityRebuildable;
            modsys.allStableBlockEntities.Add(ber.Pos);

            //Call it immediately to prevent a case where GlobalStabilitySystem tries to reference anything before the 5 seconds timer occurs
            // dt is of no consequence to the function (and technically 0 is correct I think)
            // no idea if this will ever be a problem
            // turns out this breaks shit idiot
            //QueryAndUpdateCurrentStability(0);

            /*if (Blockentity.Block.Variant.Contains(new KeyValuePair<string, string>("repairstate", "repaired")))  
            {
                
                Debug.WriteLine("It's repaired!");
            }*/

            int berRepairedBlockID = api.World.GetBlock(Blockentity.Block.CodeWithVariant("repairstate", "repaired")).Id;

            if (ber != null && Blockentity.Block.Id == berRepairedBlockID)
            {
                curStability = maxStability;
                modsys.stabilityContributors.Add(ber.Pos);
            }
            else
            {
                Debug.WriteLine("It's NOT repaired!");
            }
        }

        /*public void QueryAndUpdateCurrentStability(float dt)
        {
            //check that this is actually a rebuildable block
            if (ber != null)
            {
                //if the block is already fully repaired and the stability is already set to max, we don't care to check again
                if (ber.rebuildStage == ber.maxStage && curStability == maxStability) { return; }

                //if the block is rebuilt but our current stability is still zero, correct it and add the block to the list of contributors
                if (ber.rebuildStage == ber.maxStage && curStability == 0)
                {
                    curStability = maxStability;
                    modsys.stabilityContributors.Add(ber.Pos);
                }
                //if the block is not rebuilt and our current stability is not zero, correct it and make sure we're not in the list of contributors
                else if (ber.rebuildStage != ber.maxStage && curStability != 0)
                {
                    curStability = 0;
                    bool result = modsys.stabilityContributors.Remove(ber.Pos);
                    if (result == true) 
                    { 
                        Debug.WriteLine("ber was removed"); 
                    } 
                    else 
                    { 
                        Debug.WriteLine("ber was not removed"); 
                    }
                }
            }
            //just in case someone is a doofus
            else
            {
                curStability = maxStability;
                modsys.stabilityContributors.Add(ber.Pos);
            }
        }*/

        public void RemoveContributor()
        {
            if (ber != null)
            {
                curStability = 0;
                modsys.stabilityContributors.Remove(ber.Pos);
                


            }
        }

        public void AddContributor()
        {
            if (ber != null)
            {
                curStability = maxStability;
                modsys.stabilityContributors.Add(ber.Pos);
            }
        }

        //when the block is deleted, make sure it gets removed ASAP.
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            modsys.stabilityContributors.Remove(ber.Pos);
            modsys.allStableBlockEntities.Remove(ber.Pos);
        }
    }
}
