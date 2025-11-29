using Rustwall.RWBehaviorRebuildable;
//using Rustwall.RWBlockBehavior;
using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWItem;
using Vintagestory.API.Common;

namespace Rustwall.ModSystems.RebuildableBlock
{
    public class RebuildableBlockSystem : RustwallModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("BlockEntityRebuildable", typeof(BlockEntityRebuildable));
            api.RegisterBlockBehaviorClass("BehaviorRebuildable", typeof(BehaviorRebuildable));
            //api.RegisterBlockBehaviorClass("BehaviorDeconstructContents", typeof(BehaviorDeconstructContents));
            api.RegisterItemClass("ItemJonasScrap", typeof(ItemJonasScrap));
            api.RegisterItemClass("ItemAdminWrench", typeof(ItemAdminWrench));
        }

        protected override void RustwallStartServerSide()
        {
            
        }




    }
}
