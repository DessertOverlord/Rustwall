//using Rustwall.RWBlockBehavior;
using Vintagestory.API.Common;
using Vintagestory.API.Server;


namespace Rustwall.RWBlockEntity.BERebuildable
{
    public class BlockEntitySimpleRebuildable : BERebuildable
    {
        public override EnumRebuildableBlockType RebuildableBlockType { get { return EnumRebuildableBlockType.Simple; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Block.Variant["repairstate"] == "repaired")
            {
                CurentRebuildStage = MaxStage;
            }

            if (CurentRebuildStage > 0)
            {
                AddContributor();
            }

            if (Animatible)
            {
                InitAnimations(api);
                if (CurentRebuildStage > 0)
                {
                    ActivateAnimations();
                }
            }
        }

        public override bool DamageOneStage(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (CurentRebuildStage < 0) { return false; }

            if (CurentRebuildStage > 0)
            {
                world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), Pos, -0.25, null, true, 16);

                CurentRebuildStage--;
                ItemsUsedThisStage = 0;

                //We only want to make it appear broken if it is fully broken, not partially damaged.
                //We want to remove a contributor only if it is fully destroyed.
                if (CurentRebuildStage == 0)
                {
                    DamageFully(world, byPlayer, blockSel);
                }

                MarkDirty(true);
                return true;
            }

            return false;
        }

        public override void DamageFully(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), Pos, -0.25, byPlayer, true, 16);

            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "broken")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, Pos);

            RemoveContributor();

            if (Animatible)
            {
                /// We have to use packet broadcasts so that when the ModSystem (running only server-side) calls to damage the block,
                /// the animation change gets synchronized to the client.
                (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.DeactivateAnimations);
            }

            MarkDirty(true);

            CurentRebuildStage = 0;
            ItemsUsedThisStage = 0;
        }

        public override bool RepairByOneItem(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            slot.TakeOut(1);
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);
            slot.MarkDirty();
            ItemsUsedThisStage++;

            MarkDirty(true);

            if (ItemsUsedThisStage >= OwnBehavior.quantityPerStage[CurentRebuildStage])
            {
                RepairByOneStage(world, slot, blockSel, byPlayer);
            }

            return true;
        }

        public override bool RepairByOneStage(IWorldAccessor world, ItemSlot slot, BlockSelection blockSel, IPlayer byPlayer)
        {
            world.PlaySoundAt(new AssetLocation("sounds/effect/latch"), blockSel.Position, -0.25, byPlayer, true, 16);

            world.Api.Logger.Event("RepairByOneStage executed on " + world.Api.Side);

            slot.MarkDirty();
            ItemsUsedThisStage = 0;
            CurentRebuildStage++;

            if (CurentRebuildStage == MaxStage)
            {
                RepairFully(world);
            }
            else
            {
                GracePeriodExpirationDate = world.Calendar.ElapsedDays + GracePeriodDurationRepairOneStage;
            }

            //Simple machines should contribute and be animated even if they aren't fully repaired.
            if (CurentRebuildStage > 0)
            {
                AddContributor();
                if (Animatible)
                {
                    (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.ActivateAnimations);
                }
            }

            MarkDirty(true);
            return true;
        }

        public override void RepairFully(IWorldAccessor world)
        {
            int newBlockID = world.GetBlock(Block.CodeWithVariant("repairstate", "repaired")).Id;
            world.BlockAccessor.ExchangeBlock(newBlockID, Pos);
            AddContributor();
            if (Animatible)
            {
                //ActivateAnimations();
                (world.Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, (int)EnumRebuildableBlockPacket.ActivateAnimations);
            }
            MarkDirty(true);

            GracePeriodExpirationDate = world.Calendar.ElapsedDays + GracePeriodDurationRepairFully;
        }
    }
}
