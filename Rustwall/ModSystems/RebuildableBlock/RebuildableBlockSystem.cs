using Rustwall.RWBehaviorRebuildable;
using Rustwall.RWBlockBehavior;
using Rustwall.RWBlockEntity;
using Rustwall.RWBlockEntity.BERebuildable;
using Rustwall.RWBlockEntity.RustwallMachinery;
using Rustwall.RWItem;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace Rustwall.ModSystems.RebuildableBlock
{
    public class RebuildableBlockSystem : RustwallModSystem
    {
        public override void Start(ICoreAPI api)
        {
            //Deprecated
            //api.RegisterBlockEntityClass("BlockEntityRebuildable", typeof(BlockEntityRebuildable));
            api.RegisterBlockEntityClass("BlockEntitySimpleRebuildable", typeof(BlockEntitySimpleRebuildable));
            api.RegisterBlockEntityClass("BlockEntityComplexRebuildable", typeof(BlockEntityComplexRebuildable));
            api.RegisterBlockEntityClass("BlockEntityPumpUnit", typeof(BlockEntityPumpUnit));
            api.RegisterBlockEntityClass("BlockEntityGearbox", typeof(BlockEntityGearbox));
            //api.RegisterBlockEntityClass("BlockEntityPunchCardMachine", typeof(BlockEntityPunchCardMachine));
            api.RegisterBlockBehaviorClass("BehaviorRebuildable", typeof(BehaviorRebuildable));
            //api.RegisterBlockBehaviorClass("BehaviorPunchCardMachine", typeof(BehaviorPunchCardMachine));
            api.RegisterItemClass("ItemJonasScrap", typeof(ItemJonasScrap));
            api.RegisterItemClass("ItemAdminWrench", typeof(ItemAdminWrench));
            api.RegisterItemClass("ItemPunchCard", typeof(ItemPunchCard));

            /*sapi.ChatCommands
                .Create("addpunchtext")
                .WithArgs(sapi.ChatCommands.Parsers.All("text"))
                .HandleWith((args) =>
                {
                    

                    return TextCommandResult.Success();
                });*/
        }

        protected override void RustwallStartServerSide()
        {
            
        }




    }
}
