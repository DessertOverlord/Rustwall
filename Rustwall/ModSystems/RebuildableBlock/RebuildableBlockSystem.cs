using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWBlockBehavior;
using Rustwall.RWBlockEntity.BERebuildable;
using Vintagestory.API.Common;

namespace Rustwall.ModSystems.RebuildableBlock
{
    public class RebuildableBlockSystem : RustwallModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("BlockEntityRebuildable", typeof(BlockEntityRebuildable));
            api.RegisterBlockBehaviorClass("BehaviorRebuildable", typeof(BehaviorRebuildable));
            api.RegisterBlockBehaviorClass("BehaviorDeconstructContents", typeof(BehaviorDeconstructContents));
        }

        protected override void RustwallStartServerSide()
        {
            
        }




    }
}
