using Rustwall.RWBlockEntity.BERebuildable;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Rustwall.ModSystems.GlobalStability;
using System.Diagnostics;

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

        //public int maxStability { get; private set; }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            maxStability = properties["value"].AsInt();

            //We need to poll the current stability every so often
            Blockentity.RegisterGameTickListener(QueryAndUpdateCurrentStability, 5000);
            modsys = api.ModLoader.GetModSystem("Rustwall.ModSystems.GlobalStability.GlobalStabilitySystem") as GlobalStabilitySystem;
            //will return null if the BlockEntity is not BlockEntityRebuildable!
            ber = Blockentity as BlockEntityRebuildable;
            modsys.allStableBlockEntities.Add(ber);
        }

        public void QueryAndUpdateCurrentStability(float dt)
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
                    modsys.stabilityContributors.Add(ber);
                }
                //if the block is not rebuilt and our current stability is not zero, correct it and make sure we're not in the list of contributors
                else if (ber.rebuildStage != ber.maxStage && curStability != 0)
                {
                    curStability = 0;
                    modsys.stabilityContributors.Remove(ber);
                }
            }
            //just in case someone is a doofus
            else
            {
                curStability = maxStability;
                modsys.stabilityContributors.Add(ber);
            }
        }

        //when the block is dleted, make sure it gets removed ASAP.
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            modsys.stabilityContributors.Remove(ber);
            modsys.allStableBlockEntities.Remove(ber);
        }
    }
}
