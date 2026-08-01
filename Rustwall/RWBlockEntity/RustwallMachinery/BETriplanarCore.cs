using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Rustwall.RWBlockEntity.RustwallMachinery
{
    public class BlockEntityTriplanarCore : BlockEntityComplexRebuildable
    {
        /// <summary>
        /// Invoke explosion when fully damaged
        /// </summary>
        /// <param name="world"></param>
        /// <param name="byPlayer"></param>
        /// <param name="blockSel"></param>
        public override void DamageFully(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.DamageFully(world, byPlayer, blockSel);

            if (world.Api.Side == EnumAppSide.Server)
            {
                Explode(world.Api, Pos);
            }
        }

        public void Explode(ICoreAPI Api, BlockPos Pos)
        {
            int blastRadius = 4;
            int injureRadius = 16;
            EnumBlastType blastType = EnumBlastType.EntityBlast;
            ((IServerWorldAccessor)Api.World).CreateExplosion(Pos.Copy().Up(), blastType, blastRadius, injureRadius);
        }
    }
}
