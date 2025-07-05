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

            Blockentity.RegisterGameTickListener(QueryAndUpdateCurrentStability, 5000);
            modsys = api.ModLoader.GetModSystem("Rustwall.ModSystems.GlobalStability.GlobalStabilitySystem") as GlobalStabilitySystem;
            ber = Blockentity as BlockEntityRebuildable;
            modsys.allStableBlockEntities.Add(ber);
        }

        public void QueryAndUpdateCurrentStability(float dt)
        {
            if (ber != null)
            {
                if (ber.rebuildStage == ber.maxStage && curStability == maxStability) { return; }

                if (ber.rebuildStage == ber.maxStage && curStability == 0)
                {
                    curStability = maxStability;
                    modsys.stabilityContributors.Add(ber);
                    Debug.WriteLine("Added contributor");
                }
                else if (ber.rebuildStage != ber.maxStage && curStability != 0)
                {
                    curStability = 0;
                    modsys.stabilityContributors.Remove(ber);
                    Debug.WriteLine("Removed contributor");
                }
            }
            else
            {
                curStability = maxStability;
                modsys.stabilityContributors.Add(ber);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            modsys.stabilityContributors.Remove(ber);
            modsys.allStableBlockEntities.Remove(ber);
        }
    }
}
