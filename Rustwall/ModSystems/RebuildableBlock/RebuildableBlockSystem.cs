using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Rustwall.RWBlockEntity;
using Rustwall.RWBlockEntity.BETestRebuildable;
using Rustwall.RWBlock;
using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWEntityBehavior;

namespace Rustwall.ModSystems.RebuildableBlock
{
    /*public enum EnumRebuildState
    {
        Broken,
        Damaged,
        Scratched,
        Functional,
    }*/
    public class RebuildableBlockSystem : RustwallModSystem
    {
        public override void Start(ICoreAPI api)
        {
            //api.RegisterBlockClass("BlockTestRebuildable", typeof(BlockTestRebuildable));
            api.RegisterBlockEntityClass("BlockEntityRebuildable", typeof(BlockEntityRebuildable));
            api.RegisterBlockBehaviorClass("BehaviorRebuildable", typeof(BehaviorRebuildable));
            //api.RegisterEntityBehaviorClass("BEBehaviorRebuildable", typeof(BEBehaviorRebuildable));
        }

        protected override void RustwallStartServerSide()
        {
            //sapi.RegisterBlock();

        }




    }
}
