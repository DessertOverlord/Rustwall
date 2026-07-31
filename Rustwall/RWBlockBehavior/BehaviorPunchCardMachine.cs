using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;

namespace Rustwall.RWBlockBehavior
{
    public class BehaviorPunchCardMachine : BlockBehavior
    {
        BehaviorPunchCardMachine(Block block) : base(block)
        {

        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);

            /// If we have an unpunched card, insert it.
            /// If we don't, attempt to retrieve the current punch card.
            ///     Only if it's punched?
            /// If there's no punchcard inserted, throw an error to the player.
        }






    }
}
