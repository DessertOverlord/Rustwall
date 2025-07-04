using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rustwall.ModSystems.RebuildableBlock;
using Rustwall.RWBlock;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace Rustwall.RWBlockEntity.BETestRebuildable
{
    public class BlockEntityRebuildable : BlockEntity
    {
        //public EnumRebuildState rebuildState = EnumRebuildState.Broken;
        public int rebuildStage;
        public int itemsUsedThisStage;
        public BlockTestRebuildable ownBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockTestRebuildable;

        }

        public void DealDamage()
        {
            if (rebuildStage == 0) { return; }
        }

        public void RepairOneStage()
        {


            rebuildStage++;
            itemsUsedThisStage = 0;
            MarkDirty(true);
        }

        public void RepairOneItem()
        {
            /*itemsUsedThisStage++;
            if (itemsUsedThisStage >=   .quantityPerStage[be.rebuildStage])
            {
                be.rebuildStage++;
                be.itemsUsedThisStage = 0;
                be.MarkDirty(true);
            }*/



        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("rebuildStage", rebuildStage);
            tree.SetInt("itemsUsedThisStage", itemsUsedThisStage);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            //base.FromTreeAttributes(tree, worldAccessForResolve);

            rebuildStage = tree.GetAsInt("rebuildStage");
            itemsUsedThisStage = tree.GetAsInt("itemsUsedThisStage");
        }



    }
}
