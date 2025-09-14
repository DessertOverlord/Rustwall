using ProtoBuf;
using Rustwall.ModSystems.RingedGenerator;
using Rustwall.ModSystems.TemporalStormHandler;
using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWEntityBehavior;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

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
        public List<BlockPos> stabilityContributors { get; set; } = new List<BlockPos>();
        private List<BlockPos> previousStabilityContributors { get; set; } = new List<BlockPos>();
        public List<BlockPos> allStableBlockEntities { get; set; } = new List<BlockPos> { };
        private List<BlockPos> previousStableBlockEntities { get; set; } = new List<BlockPos>();

        //private int ;
        public override void Start(ICoreAPI api)
        {
            //register our junk
            api.RegisterBlockEntityBehaviorClass("BehaviorGloballyStable", typeof(BEBehaviorGloballyStable));
            
            
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
                data = SerializerUtil.Deserialize<globalStabilityRuntimeData>(serData);
            }
            catch (Exception)
            {
                sapi.World.Logger.Error("Failed loading global stability data, will initialize new data set");
            }

            sapi.Event.GameWorldSave += Event_GameWorldSave;

            sapi.Event.SaveGameLoaded += () =>
            {
                if (sapi.WorldManager.SaveGame.IsNew || data == null)
                {
                    sapi.World.Logger.Error("Failed loading global stability data, will initialize new data set");

                    data = new globalStabilityRuntimeData()
                    {
                        nextScoringDays = config.DaysBetweenStormScoring + sapi.World.Calendar.TotalDays,
                        nextGreatDecayDays = config.DaysBeforeTheGreatDecay + sapi.World.Calendar.TotalDays,
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
                foreach (var bePos in allStableBlockEntities)
                {
                    //var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    var beb = sapi.World.BlockAccessor.GetBlockEntity(bePos)?.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    possibleGlobalStability += beb?.maxStability != null ? beb.maxStability : 0;
                }
                //add our current working list to the previous list, for future checking
                previousStableBlockEntities = new List<BlockPos> { };
                previousStableBlockEntities.AddRange(allStableBlockEntities);
            }

            if (!stabilityContributors.SequenceEqual(previousStabilityContributors))
            {
                globalStability = 0;
            
                foreach (var bePos in stabilityContributors)
                {
                    //var beb = be.Behaviors.ToList().Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    var beb = sapi.World.BlockAccessor.GetBlockEntity(bePos)?.Behaviors.Find(x => x.GetType() == typeof(BEBehaviorGloballyStable)) as BEBehaviorGloballyStable;
                    globalStability += beb?.curStability != null ? beb.curStability : 0;
                }
                previousStabilityContributors = new List<BlockPos> { };
                previousStabilityContributors.AddRange(stabilityContributors);
            }

            if (possibleGlobalStability <= 0) { globalStabilityRatio = 0; return; }

            globalStabilityRatio = ((float)globalStability / possibleGlobalStability);
            
            //Assess scoring of the global stability and store the result
            if (data.nextScoringDays - sapi.World.Calendar.TotalDays < 0)
            {
                int numSamples = 1;
                if (sapi.World.Calendar.TotalDays - data.nextScoringDays > config.DaysBetweenStormScoring)
                {
                    numSamples = (int)(data.nextScoringDays - sapi.World.Calendar.TotalDays / config.DaysBetweenStormScoring);
                }

                if (numSamples > config.DaysBeforeTheGreatDecay / config.DaysBetweenStormScoring ) { numSamples = (int)(config.DaysBeforeTheGreatDecay / config.DaysBetweenStormScoring); }

                for (int i = 0; i < numSamples; i++)
                {
                    data.nextScoringDays = data.nextScoringDays + config.DaysBetweenStormScoring;
                    data.scores.Add(globalStabilityRatio);
                    Debug.WriteLine("Score of " + globalStabilityRatio + " Added to score list");
                }
                //data.nextScoringDays = sapi.World.Calendar.TotalDays + config.DaysBetweenStormScoring;
            }

            //Assess great decay
            if (data.nextGreatDecayDays - sapi.World.Calendar.TotalDays < 0)
            {
                float totalScore = 0;
                foreach (var item in data.scores) { totalScore += item; }
                float averageScore = totalScore / data.scores.Count;
                data.scores.Clear();

                data.nextGreatDecayDays = sapi.World.Calendar.TotalDays + config.DaysBeforeTheGreatDecay;
                var ringedGenModSys = sapi.ModLoader.GetModSystem<RingedGeneratorSystem>();
                ringedGenModSys.TriggerGreatDecay(1.0f - averageScore);
                Debug.WriteLine("Great decay triggered with average score of: " + averageScore);
            }

            //For all contributing blocks, we need to roll the dice on damaging them by a stage.
            foreach (var item in stabilityContributors.ToList())
            {
                //Check if the contributor is actually a rebuildable block. Adds functionality for the future for unbreakable stability contributors.
                // Also check if it's already destroyed -- no reason to run all of this code if it's already broken.
                // We ALSO want to check if the machine is a complex machine -- if the complex machine is not fully repaired, don't break it.
                BlockEntityRebuildable RBitem = sapi.World.BlockAccessor.GetBlockEntity(item) as BlockEntityRebuildable;
                if (RBitem == null || RBitem.rebuildStage == 0 || RBitem.repairLock == false) { continue; }

                double damageChanceMultiplier;
                if (sapi.ModLoader.GetModSystem<TemporalStormHandlerSystem>().IsStormActive())
                {
                    damageChanceMultiplier = config.TemporalStormDamageMultiplier;
                }
                else
                {
                    damageChanceMultiplier = 1;
                }

                Random rand = new Random();
                //If this item is a simple machine (can be repaired at any time), we need to use a different range of random values
                if (RBitem.ownBehavior.canRepairBeforeBroken)
                {
                    //This gives us a 1/288 chance every 10 seconds to damage the block. In theory, this should mean a block gets damage ~once every in-game day.
                    //Diving by damageChanceMultiplier means that it is 5x more likely to hit the random chance.
                    //if (rand.Next(288 / damageChanceMultiplier) == 0)
                    if (rand.Next((int)(config.ChanceToBreakSimple / damageChanceMultiplier)) == 0)
                    {
                        //Debug.WriteLine("Damaged shit by one stage. Multiplier is: " + damageChanceMultiplier);
                        //Feeding nulls into this function is okay because IPlayer and BlockSel are only used to create sounds; for our purposes, they are not needed.
                        RBitem.ownBehavior.DamageOneStage(sapi.World, null, RBitem, null);
                    }
                }
                else
                {
                    if (rand.Next((int)(config.ChanceToBreakComplex / damageChanceMultiplier)) == 0)
                    {
                        RBitem.ownBehavior.DamageOneStage(sapi.World, null, RBitem, null);
                    }
                }
            }
        }
    }
}
