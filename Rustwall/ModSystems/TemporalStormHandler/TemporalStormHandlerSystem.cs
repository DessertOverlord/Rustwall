using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using HarmonyLib;
using System.Diagnostics;
using System.Collections.Generic;
using System;

namespace Rustwall.ModSystems.TemporalStormHandler
{
    internal class TemporalStormHandlerSystem : RustwallModSystem
    {
        ICoreServerAPI sapi;

        public enum EnumStormClimate
        {
            Relaxed,
            Tense,
            Aggressive,
        }

        protected override void RustwallStartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            RegisterChatCommands();

            // Initialize Harmony for our postfix
            var harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();

            // Add HandleEntityDeath to the event so it gets triggered
            sapi.Event.OnEntityDeath += HandleEntityDeath;
        }

        //This runs after any entity dies
        // TODO: Check if entity is a valid rust mob (drifter, shiver, bowtorn) before subtracting any time
        // DONE
        // TODO: Check to see if a storm is actually active before doing anything
        // DONE
        private void HandleEntityDeath(Entity entity, DamageSource damageSource)
        {
            //These vars represent minutes and hours in units of days as a double
            //double tempStormTimeRemovalMin = 0.000694;
            double tempStormTimeRemovalHour = 0.0417;
            //Check if damage originated from a player -- no, you can't kill shit with fall damage
            if (damageSource.Source == EnumDamageSource.Player &&
                Patch_onTempStormTick.tStormData.stormActiveTotalDays - sapi.World.Calendar.TotalDays > 0 &&
                  (entity.GetName().Contains("drifter") ||
                  entity.GetName().Contains("shiver") ||
                  entity.GetName().Contains("bowtorn")))
            {
                //Add the offset; onTempStormTick only runs every 2 seconds, so we need a buffer in case players kill multiple mobs inside of the 2 second window
                Patch_onTempStormTick.currentStormOffset += tempStormTimeRemovalHour;
                Patch_onTempStormTick.writeData = true;
                Debug.WriteLine("Time subtracted");
            }
        }

        private void RegisterChatCommands()
        {
            sapi.ChatCommands.Create("moon")
                .WithDescription("Retrieves current rough & exact moon phase")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(handler => TextCommandResult.Success("Current Moon Phase: " + sapi.World.Calendar.MoonPhase + ", Exact: " + sapi.World.Calendar.MoonPhaseExact));
        }

    }

    //We need to postfix the temporal storm ticking method to access the runtime data that the game uses for storms
    [HarmonyPatch(typeof(SystemTemporalStability), "onTempStormTick")]
    internal static class Patch_onTempStormTick
    {
        //These fields are accessible from our modSystem
        public static double currentStormOffset;
        public static bool writeData = false;
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
            if (writeData)
            {
                //Subtract the current offset from the timer the game uses.
                // It is important to note that the game tracks the end of a temporal storm in terms of absolute calendar days,
                // not by a relative number of days or hours.
                ___data.stormActiveTotalDays -= currentStormOffset;
                writeData = false;
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
            //double curExactMoonPhase = ___sapi.World.Calendar.MoonPhaseExact;
            //double totalDaysUntilNextFullMoon = ___sapi.World.Calendar.TotalDays + ((8 - curExactMoonPhase) * 2) % 16;

            /*___data.nextStormStrength = EnumTempStormStrength.Heavy;
            ___data.nextStormStrDouble = 2;
            ___data.nextStormTotalDays = totalDaysUntilNextFullMoon;
            ___data.stormActiveTotalDays = 0;*/

            var climate = TemporalStormHandlerSystem.EnumStormClimate.Relaxed;


        }
    }


}
