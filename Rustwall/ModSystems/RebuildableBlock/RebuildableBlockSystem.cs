using Rustwall.RWBehaviorRebuildable;
//using Rustwall.RWBlockBehavior;
using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWBlockEntity.RustwallMachinery;
using Rustwall.RWItem;
using Vintagestory.API.Common;

namespace Rustwall.ModSystems.RebuildableBlock
{
    public class RebuildableBlockSystem : RustwallModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("BlockEntityRebuildable", typeof(BlockEntityRebuildable));
            api.RegisterBlockEntityClass("BlockEntityPumpUnit", typeof(BlockEntityPumpUnit));
            api.RegisterBlockEntityClass("BlockEntityGearbox", typeof(BlockEntityGearbox));
            api.RegisterBlockBehaviorClass("BehaviorRebuildable", typeof(BehaviorRebuildable));
            api.RegisterItemClass("ItemJonasScrap", typeof(ItemJonasScrap));
            api.RegisterItemClass("ItemAdminWrench", typeof(ItemAdminWrench));
        }

        protected override void RustwallStartServerSide()
        {
            
        }




    }
}
