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
        public List<BlockEntity> stabilityContributors { get; set; } = new List<BlockEntity>();
        public List<BlockEntity> previousStabilityContributors { get; set; } = new List<BlockEntity>();
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
                    return TextCommandResult.Success("Current Stability: " + globalStability);
                });
        }

        private void EvaluateGlobalStability(float dt)
        {
            if (stabilityContributors.SequenceEqual(previousStabilityContributors)) { return; }
            globalStability = 0;
            foreach (var be in stabilityContributors)
            {
                var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                if (beb != null )
                {
                    globalStability += beb.curStability;
                    //Debug.WriteLine();
                }
            }
            previousStabilityContributors = new List<BlockEntity> { };
            previousStabilityContributors.AddRange(stabilityContributors);
            Debug.WriteLine("EvalGlobalStab Ran.");
        }
    }
}
