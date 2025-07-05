using Vintagestory.API.Common;
using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWBehaviorRebuildable;

namespace Rustwall.ModSystems.RebuildableBlock
{
    public class RebuildableBlockSystem : RustwallModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("BlockEntityRebuildable", typeof(BlockEntityRebuildable));
            api.RegisterBlockBehaviorClass("BehaviorRebuildable", typeof(BehaviorRebuildable));
        }

        protected override void RustwallStartServerSide()
        {

        }




    }
}
