using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWEntityBehavior;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Rustwall.ModSystems.GlobalStability
{
    internal class GlobalStabilitySystem : RustwallModSystem
    {
        public int globalStability { get; private set; } = 0;
        public int possibleGlobalStability { get; private set; } = 0;
        public float globalStabilityRatio { get; private set; } = 0;
        public List<BlockEntity> stabilityContributors { get; set; } = new List<BlockEntity>();
        private List<BlockEntity> previousStabilityContributors { get; set; } = new List<BlockEntity>();
        public List<BlockEntity> allStableBlockEntities { get; set; } = new List<BlockEntity> { };
        private List<BlockEntity> previousStableBlockEntities { get; set; } = new List<BlockEntity>();
        public override void Start(ICoreAPI api)
        {
            //api.RegisterBlockBehaviorClass("BehaviorGloballyStable", typeof(BehaviorGloballyStable));
            api.RegisterBlockEntityBehaviorClass("BehaviorGloballyStable", typeof(BEBehaviorGloballyStable));
            api.Event.RegisterGameTickListener(EvaluateGlobalStability, 10000);
        }

        protected override void RustwallStartServerSide()
        {
            RegisterChatCommands();
        }

        private void RegisterChatCommands()
        {
            sapi.ChatCommands.Create("gstab")
                .WithDescription("I UNNO")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .WithArgs()
                .HandleWith((args) =>
                {
                    return TextCommandResult.Success("Current Stability: " + globalStability + 
                                                    "\nPossible Stability: " + possibleGlobalStability + 
                                                    "\nStability ratio: " + globalStabilityRatio);
                });
        }

        private void EvaluateGlobalStability(float dt)
        {
            if (!allStableBlockEntities.SequenceEqual(previousStableBlockEntities)) {
                possibleGlobalStability = 0;
                foreach (var be in allStableBlockEntities)
                {
                    var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    possibleGlobalStability += beb.maxStability;
                }

                previousStableBlockEntities = new List<BlockEntity> { };
                previousStableBlockEntities.AddRange(allStableBlockEntities);
            }

            if (!stabilityContributors.SequenceEqual(previousStabilityContributors))
            {
                globalStability = 0;
            
                foreach (var be in stabilityContributors)
                {
                    var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    if (beb != null )
                    {
                        globalStability += beb.curStability;
                    }
                }
                previousStabilityContributors = new List<BlockEntity> { };
                previousStabilityContributors.AddRange(stabilityContributors);
            }

            globalStabilityRatio = ((float)globalStability / possibleGlobalStability);

            Debug.WriteLine("EvalGlobalStab Ran.");
        }
    }
}
