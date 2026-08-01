using Rustwall.ModSystems.GlobalStability;
using Rustwall.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Rustwall.RWBlockBehavior
{
    public class BehaviorPunchCardMachine : BlockBehavior
    {
        public BehaviorPunchCardMachine(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {

            /// If we have an unpunched card, insert it.
            /// If we don't, attempt to retrieve the current punch card.
            ///     Only if it's punched?
            /// If there's no punchcard inserted, throw an error to the player.

            /// SIMPLIFY: We're just going to click with an unpunched card and get back a punched card for now.

            handling = EnumHandling.PreventSubsequent;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot?.Itemstack?.Collectible?.Code == "rustwall:punchcard-unpunched")
            {
                if (world.Side == EnumAppSide.Server)
                {
                    AssetLocation newItemCode = slot.Itemstack.Collectible.CodeWithVariant("state", "punched");
                    
                    Item newItem = world?.GetItem(newItemCode);
                    ItemStack newItemStack = new(newItem, 1);
                    string punchText = ProducePunchCardDataString((world.Api as ICoreServerAPI));
                    newItemStack.Attributes.SetBytes("punchcarddata", PunchCardUtils.EncodeString(punchText));
                    slot.Itemstack.SetFrom(newItemStack);
                    //(byPlayer as IServerPlayer).InventoryManager.TryGiveItemstack(newItemStack, true);
                }
                slot.MarkDirty();
                return true;
            }
            else
            {
                (byPlayer as IServerPlayer)?.SendIngameError("rustwall:interact-needpunchcard");
                return true;
            }
        }

        public string ProducePunchCardDataString(ICoreServerAPI sapi)
        {
            var gStabSys = sapi.ModLoader.GetModSystem<GlobalStabilitySystem>();
            float stabPercent = gStabSys.globalStabilityRatio;
            int totalMachines = gStabSys.allStableBlockEntities.Count;
            int brokenMachines = totalMachines - gStabSys.stabilityContributors.Count;
            string timestamp = ProduceCurrentTimeStamp(sapi);
            string output = $"TMSTMP: {timestamp} | STAB: {stabPercent * 100:000.0}% | TOTL MACH: {totalMachines:0000} | BRKN MACH: {brokenMachines:0000}";
            //"TMSTMP: 0000 00 00 @ 00 00 | STAB: 000.0% | TOTL MACH: 0000 | BRKN MACH: 0000";


            return output;
        }

        public string ProduceCurrentTimeStamp(ICoreServerAPI sapi)
        {
            string output = "";

            int year = sapi.World.Calendar.Year;
            int month = sapi.World.Calendar.Month;
            int day = sapi.World.Calendar.DayOfYear - ((sapi.World.Calendar.DayOfYear / sapi.World.Calendar.DaysPerMonth) * sapi.World.Calendar.DaysPerMonth);
            int hour = sapi.World.Calendar.FullHourOfDay;
            int minute = (int)((sapi.World.Calendar.HourOfDay - hour) * 60);

            output = $"{year:0000} {month:00} {day:00} @ {hour:00} {minute:00}";

            return output;
        }
    }
}
