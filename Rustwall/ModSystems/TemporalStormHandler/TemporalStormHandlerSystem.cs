using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using HarmonyLib;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using Vintagestory.API.Util;
using System.ComponentModel.Design;
using ProtoBuf;
using Rustwall.Configs;

namespace Rustwall.ModSystems.TemporalStormHandler
{
    internal class TemporalStormHandlerSystem : RustwallModSystem
    {
        //ICoreServerAPI sapi;
        //TemporalStormConfig config;
        //public readonly bool isStormActive;


        internal enum EnumStormClimate
        {
            Relaxed,
            Sporadic,
            Aggressive,
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class StormClimateRuntimeData
        {
            public EnumStormClimate currentStormClimate { get; internal set; } = EnumStormClimate.Sporadic;
            public int stormsUntilClimateShift { get; internal set; } = 2;
        }

        internal static StormClimateRuntimeData runtimeData { get; set; } = new StormClimateRuntimeData();

        protected override void RustwallStartServerSide()
        {
            //sapi = api;
            RegisterChatCommands();

            // Initialize Harmony for our postfix
            var harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();

            // Add our listeners to their events so they get triggered
            sapi.Event.OnEntityDeath += Event_OnEntityDeath;
            sapi.Event.SaveGameLoaded += Event_SaveGameLoaded;
            sapi.Event.GameWorldSave += Event_GameWorldSave;

            //config = sapi.LoadModConfig<TemporalStormConfig>("rustwall_temporalstormconfig.json");
            var testvar = config.TemporalStormDaysRemovedPerKill;

        }

        //These two handle saving the current state of the stormClimate for when the server is restarted
        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("stormClimate", SerializerUtil.Serialize(runtimeData));
        }

        private void Event_SaveGameLoaded()
        {
            try
            {
                runtimeData = SerializerUtil.Deserialize<StormClimateRuntimeData>(sapi.WorldManager.SaveGame.GetData("stormClimate"));
            }
            catch (Exception)
            {
                sapi.Logger.Error("Storm climate data unable to be loaded");
                runtimeData = new StormClimateRuntimeData();
            }
        }

        //This runs after any entity dies
        // TODO: Check if entity is a valid rust mob (drifter, shiver, bowtorn) before subtracting any time
        // DONE
        // TODO: Check to see if a storm is actually active before doing anything
        // DONE
        private void Event_OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            //These vars represent minutes and hours in units of days as a double
            //double tempStormTimeRemovalMin = 0.000694;
            //double tempStormTimeRemovalHour = 0.0417;
            //Check if damage originated from a player -- no, you can't kill shit with fall damage
            if (damageSource?.Source == EnumDamageSource.Player &&
                Patch_onTempStormTick.tStormData.stormActiveTotalDays - sapi.World.Calendar.TotalDays > 0 &&
                  (entity.GetName().Contains("drifter") ||
                  entity.GetName().Contains("shiver") ||
                  entity.GetName().Contains("bowtorn")))
            {
                //Add the offset; onTempStormTick only runs every 2 seconds, so we need a buffer in case players kill multiple mobs inside of the 2 second window
                Patch_onTempStormTick.currentStormOffset += config.TemporalStormDaysRemovedPerKill;
                //Patch_onTempStormTick.writeData = true;
                Debug.WriteLine("Time subtracted");
            }
        }

        private void RegisterChatCommands()
        {
            //Used to research moon phase info. Leaving unimplemented for normal deployment
            /*sapi.ChatCommands.Create("moon")
                .WithDescription("Retrieves current rough & exact moon phase")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(handler => TextCommandResult.Success("Current Moon Phase: " + sapi.World.Calendar.MoonPhase + ", Exact: " + sapi.World.Calendar.MoonPhaseExact));*/
        }

        public bool IsStormActive()
        {
            return Patch_onTempStormTick.tStormData.nowStormActive;
        }
    }

    //We need to postfix the temporal storm ticking method to access the runtime data that the game uses for storms
    [HarmonyPatch(typeof(SystemTemporalStability), "onTempStormTick")]
    internal static class Patch_onTempStormTick
    {
        //These fields are accessible from our modSystem
        public static double currentStormOffset = 0;
        //public static bool writeData = false;
        public static TemporalStormRunTimeData tStormData;
        // ___data allows us to access the field "data" from the original code
        //Below comments stop Intellisense from bitching about the fact that it can't tell Harmony does shit here
        #pragma warning disable IDE0051 // Remove unused private members
        static void Postfix(SystemTemporalStability __instance, ref TemporalStormRunTimeData ___data, ICoreServerAPI ___sapi)
        #pragma warning restore IDE0051 // Remove unused private members
        {
            // Init tStormData so that HandleEntityDeath can measure the current storm time
            if (tStormData == null)
            {
                tStormData = ___data;
            }

            // We only want to engage this to overwrite if we actually have new data, otherwise we're wasting time (and also may crash)
            if (currentStormOffset > 0)
            {
                //Subtract the current offset from the timer the game uses.
                // It is important to note that the game tracks the end of a temporal storm in terms of absolute calendar days,
                // not by a relative number of days or hours.
                ___data.stormActiveTotalDays -= currentStormOffset;
                //writeData = false;
                currentStormOffset = 0;
            }
        }
    }

    //We also need to patch the prepareNextStorm function so that we can intercept the configuration of the next temporal storm as it is made
    [HarmonyPatch(typeof(SystemTemporalStability), "prepareNextStorm")]
    internal static class Patch_prepareNextStorm
    {
        static void Postfix(SystemTemporalStability __instance, ref TemporalStormRunTimeData ___data, ICoreServerAPI ___sapi)
        {
            //TemporalStormHandlerSystem.EnumStormClimate climate = TemporalStormHandlerSystem.runtimeData.currentStormClimate;
            
            //We use this to decide what the next storm climate will be.
            var rnd = ___sapi.World.Rand.Next(3);

            //onTempStormTick uses the formula (0.1f + data.nextStormStrDouble * 0.1f) * tempstormDurationMul to compute the duration of the storm.
            //This method takes a duration and days and does the inverse of that math so that we can get our exact desired duration.
            double computeStormStrDouble(double desiredDuration)
            { 
                return (desiredDuration - 0.1) / 0.1;
            }
            //Relaxed storms are infrequent (once every two days) but last half a day (24 minutes)
            //Sporadic storms are half as long, but happen twice as often
            //Aggressive storms are much shorter, lasting only one tenth of a day (only 5 minutes), but there are 4 per irl day
            if (TemporalStormHandlerSystem.runtimeData.stormsUntilClimateShift <= 0)
            {
                TemporalStormHandlerSystem.runtimeData.currentStormClimate = (TemporalStormHandlerSystem.EnumStormClimate)rnd;
                TemporalStormHandlerSystem.runtimeData.stormsUntilClimateShift = (int)TemporalStormHandlerSystem.runtimeData.currentStormClimate * 2;
            }

            ___data.nextStormStrength = EnumTempStormStrength.Heavy;

            switch (TemporalStormHandlerSystem.runtimeData.currentStormClimate)
            {
                case TemporalStormHandlerSystem.EnumStormClimate.Relaxed:
                    ___data.nextStormStrDouble = computeStormStrDouble(0.5);
                    ___data.nextStormTotalDays = ___sapi.World.Calendar.TotalDays + 60;
                    break;
                case TemporalStormHandlerSystem.EnumStormClimate.Sporadic:
                    ___data.nextStormStrDouble = computeStormStrDouble(0.25);
                    ___data.nextStormTotalDays = ___sapi.World.Calendar.TotalDays + 30;
                    break;
                case TemporalStormHandlerSystem.EnumStormClimate.Aggressive:
                    ___data.nextStormStrDouble = computeStormStrDouble(0.1);
                    ___data.nextStormTotalDays = ___sapi.World.Calendar.TotalDays + 15;
                    break;
                default:
                    throw new Exception("EnumStormClimate shit the bed");
                    //break;
            }
            TemporalStormHandlerSystem.runtimeData.stormsUntilClimateShift--;
        }
    }
}
