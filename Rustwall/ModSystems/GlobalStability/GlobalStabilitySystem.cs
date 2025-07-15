using ProtoBuf;
using Rustwall.ModSystems.RingedGenerator;
using Rustwall.ModSystems.TemporalStormHandler;
using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWEntityBehavior;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Rustwall.ModSystems.GlobalStability
{
    internal class GlobalStabilitySystem : RustwallModSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class globalStabilityRuntimeData
        {
            public double nextScoringDays;
            public double nextGreatDecayDays;
            public List<float> scores = new List<float>();
        }

        globalStabilityRuntimeData data;
        //We do not need to store this data at runtime because it is re-assessed every time the world loads
        public int globalStability { get; private set; } = 0;
        public int possibleGlobalStability { get; private set; } = 0;
        public float globalStabilityRatio { get; private set; } = 0;
        public List<BlockEntity> stabilityContributors { get; set; } = new List<BlockEntity>();
        private List<BlockEntity> previousStabilityContributors { get; set; } = new List<BlockEntity>();
        public List<BlockEntity> allStableBlockEntities { get; set; } = new List<BlockEntity> { };
        private List<BlockEntity> previousStableBlockEntities { get; set; } = new List<BlockEntity>();


        //private int ;
        public override void Start(ICoreAPI api)
        {
            //register our junk
            api.RegisterBlockEntityBehaviorClass("BehaviorGloballyStable", typeof(BEBehaviorGloballyStable));
            
            //api.Event.RegisterGameTickListener(onGlobalStabilityTick, 10000);
        }

        private void Event_GameWorldSave()
        {
            byte[] serData = SerializerUtil.Serialize(data);
            sapi.WorldManager.SaveGame.StoreData("globalStabilityRuntimeData", serData);
        }

        protected override void RustwallStartServerSide()
        {
            RegisterChatCommands();

            try
            {
                byte[] serData = sapi.WorldManager.SaveGame.GetData("globalStabilityRuntimeData");
                data = SerializerUtil.Deserialize<globalStabilityRuntimeData>(serData);
            }
            catch (Exception)
            {
                sapi.World.Logger.Notification("Failed loading global stability data, will initialize new data set");
            }

            sapi.Event.GameWorldSave += Event_GameWorldSave;

            sapi.Event.SaveGameLoaded += () =>
            {
                if (sapi.WorldManager.SaveGame.IsNew || data == null)
                {
                    sapi.World.Logger.Notification("Failed loading global stability data, will initialize new data set");

                    data = new globalStabilityRuntimeData()
                    {
                        nextScoringDays = config.DaysBetweenStormScoring + sapi.World.Calendar.TotalDays,
                        nextGreatDecayDays = config.DaysBeforeTheGreatDecay + sapi.World.Calendar.TotalDays,
                    };
                }
            };
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

        private void onGlobalStabilityTick(float dt)
        {
            //Checks if there have actually been any changes since last time -- if not we don't care
            if (!allStableBlockEntities.SequenceEqual(previousStableBlockEntities)) {
                //reset our amount
                possibleGlobalStability = 0;
                //for everything in the list, add its maximum stability to the global pool
                foreach (var be in allStableBlockEntities)
                {
                    var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    possibleGlobalStability += beb.maxStability;
                }
                //add our current working list to the previous list, for future checking
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

            if (possibleGlobalStability <= 0) { return; }

            globalStabilityRatio = ((float)globalStability / possibleGlobalStability);

            //double nextScoring = sapi.World.Calendar.TotalDays + config.DaysBetweenStormScoring;

            if (sapi.World.Calendar.TotalDays - data.nextScoringDays < 0)
            {
                data.nextScoringDays = sapi.World.Calendar.TotalDays + config.DaysBetweenStormScoring;
                data.scores.Add(globalStabilityRatio);

                Debug.WriteLine("Score of " + globalStabilityRatio + "Added to score list");

            }

            if (sapi.World.Calendar.TotalDays - data.nextGreatDecayDays < 0)
            {
                float totalScore = 0;
                foreach (var item in data.scores) { totalScore += item; }
                float averageScore = totalScore / data.scores.Count;

                data.nextGreatDecayDays = sapi.World.Calendar.TotalDays + config.DaysBeforeTheGreatDecay;
                var ringedGenModSys = sapi.ModLoader.GetModSystem<RingedGeneratorSystem>();
                ringedGenModSys.TriggerGreatDecay(averageScore);
                Debug.WriteLine("Great decay triggered with average score of: " + averageScore);
            }

        }
    }
}
