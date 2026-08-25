using ProtoBuf;
using Rustwall.ModSystems.RingedGenerator;
using Rustwall.ModSystems.TemporalStormHandler;
using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWEntityBehavior;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Rustwall.ModSystems.GlobalStability
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class GlobalStabilityRuntimeData
    {
        public double NextScoringDays { get; set; }
        public double NextGreatDecayDays { get; set; }
        public List<float> Scores { get; set; } = [];
        public int GlobalStability { get; set; }
        public int PossibleGlobalStability { get; set; } = 0;
        public float GlobalStabilityRatio 
        { 
            get 
            { 
                if (PossibleGlobalStability <= 0 || GlobalStability <= 0) { return 0; }
                return GlobalStability / PossibleGlobalStability; 
            }
        }
        public List<BlockPos> StabilityContributors { get; set; } = [];
        public List<BlockPos> PreviousStabilityContributors { get; set; } = [];
        public List<BlockPos> AllStableBlockEntities { get; set; } = [];
        public List<BlockPos> PreviousStableBlockEntities { get; set; } = [];

        /// <summary>
        /// Checks if a given BlockPos is in the list of StabilityContributors.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool IsStabilityContributor(BlockPos pos)
        {
            return StabilityContributors.Contains(pos);
        }
    }

    public class GlobalStabilitySystem : RustwallModSystem
    {
        public GlobalStabilityRuntimeData data;

        private ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("BehaviorGloballyStable", typeof(BEBehaviorGloballyStable));
            this.api = api;
        }

        private void Event_GameWorldSave()
        {
            byte[] serData = SerializerUtil.Serialize(data);
            sapi.WorldManager.SaveGame.StoreData("globalStabilityRuntimeData", serData);
        }

        protected override void RustwallStartServerSide()
        {
            RegisterChatCommands();

            //Changed to 1 second for testing, move back to 10 seconds in prod
            //sapi.Event.RegisterGameTickListener(onGlobalStabilityTick, 10000);

            try
            {
                byte[] serData = sapi.WorldManager.SaveGame.GetData("globalStabilityRuntimeData");
                data = SerializerUtil.Deserialize<GlobalStabilityRuntimeData>(serData);
            }
            catch (Exception)
            {
                if (!sapi.WorldManager.SaveGame.IsNew)
                {
                    sapi.World.Logger.Error("Failed to load existing global stability data.");
                }
            }

            sapi.Event.GameWorldSave += Event_GameWorldSave;

            sapi.Event.SaveGameLoaded += () =>
            {
                if (sapi.WorldManager.SaveGame.IsNew || data == null)
                {
                    sapi.World.Logger.Notification("Failed to load global stability data, will initialize new data set. Normal on first world load.");

                    data = new GlobalStabilityRuntimeData()
                    {
                        NextScoringDays = Config.DaysBetweenStormScoring + sapi.World.Calendar.TotalDays,
                        NextGreatDecayDays = Config.DaysBeforeTheGreatDecay + sapi.World.Calendar.TotalDays,
                    };
                }

                sapi.Event.RegisterGameTickListener(onGlobalStabilityTick, 2000);
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
                    return TextCommandResult.Success("Current Stability: " + data.GlobalStability +
                                                    "\nPossible Stability: " + data.PossibleGlobalStability +
                                                    "\nStability ratio: " + data.GlobalStabilityRatio);
                });
        }

        private void onGlobalStabilityTick(float dt)
        {
            //Checks if there have actually been any changes since last time -- if not we don't care
            if (!data.AllStableBlockEntities.SequenceEqual(data.PreviousStableBlockEntities))
            {
                //reset our amount
                data.PossibleGlobalStability = 0;
                //for everything in the list, add its maximum stability to the global pool
                foreach (var bePos in data.AllStableBlockEntities)
                {
                    //var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    //var beb = sapi.World.BlockAccessor.GetBlockEntity(bePos)?.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    var be = sapi.World.BlockAccessor.GetBlockEntity<BERebuildable>(bePos);
                    data.PossibleGlobalStability += be?.MaxStability != null ? be.MaxStability : 0;
                }
                //add our current working list to the previous list, for future checking
                data.PreviousStableBlockEntities = [.. data.AllStableBlockEntities];
            }

            if (!data.StabilityContributors.SequenceEqual(data.PreviousStabilityContributors))
            {
                data.GlobalStability = 0;

                foreach (var bePos in data.StabilityContributors)
                {
                    //var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    //var beb = sapi.World.BlockAccessor.GetBlockEntity(bePos)?.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    var be = sapi.World.BlockAccessor.GetBlockEntity<BERebuildable>(bePos);
                    data.GlobalStability += be?.CurStability != null ? be.CurStability : 0;
                }
                data.PreviousStabilityContributors = [.. data.StabilityContributors];
            }            

            //Assess scoring of the global stability and store the result
            if (data.NextScoringDays - sapi.World.Calendar.TotalDays < 0)
            {
                int numSamples = 1;
                if (sapi.World.Calendar.TotalDays - data.NextScoringDays > Config.DaysBetweenStormScoring)
                {
                    numSamples = (int)(data.NextScoringDays - sapi.World.Calendar.TotalDays / Config.DaysBetweenStormScoring);
                }

                if (numSamples > Config.DaysBeforeTheGreatDecay / Config.DaysBetweenStormScoring) { numSamples = (int)(Config.DaysBeforeTheGreatDecay / Config.DaysBetweenStormScoring); }

                for (int i = 0; i < numSamples; i++)
                {
                    data.NextScoringDays = data.NextScoringDays + Config.DaysBetweenStormScoring;
                    data.Scores.Add(data.GlobalStabilityRatio);
                    sapi.Logger.Audit("Score of " + data.GlobalStabilityRatio + " Added to score list");
                }
            }

            //Assess great decay
            if (data.NextGreatDecayDays - sapi.World.Calendar.TotalDays < 0)
            {
                float totalScore = 0;
                foreach (var item in data.Scores) { totalScore += item; }
                float averageScore = totalScore / data.Scores.Count;
                data.Scores.Clear();

                data.NextGreatDecayDays = sapi.World.Calendar.TotalDays + Config.DaysBeforeTheGreatDecay;
                var ringedGenModSys = sapi.ModLoader.GetModSystem<RingedGeneratorSystem>();
                ringedGenModSys.TriggerGreatDecay(1.0f - averageScore, true);
                sapi.Logger.Audit("Great decay triggered with average score of: " + averageScore);
            }

            //For all contributing blocks, we need to roll the dice on damaging them by a stage.
            foreach (var item in data.StabilityContributors.ToList())
            {
                //Check if the contributor is actually a rebuildable block. Adds functionality for the future for unbreakable stability contributors.
                // Also check if it's already destroyed -- no reason to run all of this code if it's already broken.
                // We ALSO want to check if the machine is a complex machine -- if the complex machine is not fully repaired, don't break it.
                if (sapi.World.BlockAccessor.GetBlockEntity(item) is not BERebuildable RBitem || RBitem.CurentRebuildStage == 0 || (!RBitem.CanRepairBeforeBroken && RBitem.RepairLock == false)) { continue; }

                //Check to see if this item is under a grace period. If so, skip it.
                if (RBitem.IsGracePeriodActive)
                {
                    continue;
                }

                double damageChanceMultiplier;
                if (sapi.ModLoader.GetModSystem<TemporalStormHandlerSystem>().IsStormActive())
                {
                    damageChanceMultiplier = Config.TemporalStormDamageMultiplier;
                }
                else
                {
                    damageChanceMultiplier = 1;
                }

                Random rand = new();
                //If this item is a simple machine (can be repaired at any time), we need to use a different range of random values
                if (RBitem.CanRepairBeforeBroken)
                {
                    //This gives us a 1/288 chance every 10 seconds to damage the block. In theory, this should mean a block gets damage ~once every in-game day.
                    //Diving by damageChanceMultiplier means that it is 5x more likely to hit the random chance.
                    if (rand.Next((int)(Config.ChanceToBreakSimple / damageChanceMultiplier)) == 0)
                    {
                        //Feeding nulls into this function is okay because IPlayer and BlockSel are only used to create sounds; for our purposes, they are not needed.
                        RBitem.DamageOneStage(api.World, null, null);
                    }
                }
                else
                {
                    if (rand.Next((int)(Config.ChanceToBreakComplex / damageChanceMultiplier)) == 0)
                    {
                        RBitem.DamageOneStage(api.World, null, null);
                    }
                }
            }
        }
    }
}
