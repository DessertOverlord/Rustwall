using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Rustwall.RWBlockEntity
{
    public class BlockEntityPunchCardMachine : BlockEntity
    {
        public Dictionary<BlockPos, string> TrackedBlockEntities = new()
        {

        };

        public override void Initialize(ICoreAPI api)
        {
            //base.Initialize(api);

            /// Instantiate and subscribe to the list of all Punch Card Machines


        }



    }
}
