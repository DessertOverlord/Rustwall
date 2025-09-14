using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Rustwall.RWBlockBehavior
{
    internal class BehaviorDeconstructContents : BlockBehavior
    {
        
        public BehaviorDeconstructContents(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            BlockEntityGroundStorage be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;

            if (be is null) { return false; }

            if (slot.Itemstack?.Item.Code.PathStartsWith("hammer") == true && be.StorageProps.Layout == EnumGroundStorageLayout.SingleCenter)
            {
                ItemSlot beSlot = be.Inventory.FirstNonEmptySlot;

                if (beSlot.Itemstack.Item.Code.PathStartsWith("jonasparts") || beSlot.Itemstack.Item.Code.PathStartsWith("jonasframes"))
                {
                    beSlot.TakeOut(1);
                }
            }



            return true;
        }



    }
}
