using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Rustwall.RWBlockEntity.RustwallMachinery
{
    public class BlockEntityTriplanarCore : BlockEntityComplexRebuildable
    {
        //Invoke explosion when fully damaged
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

            //Api.World.BlockAccessor.SetBlock(0, Pos);
            ((IServerWorldAccessor)Api.World).CreateExplosion(Pos.Copy().Up(), blastType, blastRadius, injureRadius);
            //Api.World.BlockAccessor.SetBlock(Block.Id, Pos);

            if (Api.Side == EnumAppSide.Client)
            {
                /*SimpleParticleProperties smallSparks = new SimpleParticleProperties(
                    1, 1,
                    ColorUtil.ToRgba(255, 255, 233, 0),
                    new Vec3d(), new Vec3d(),
                    new Vec3f(-3f, 5f, -3f),
                    new Vec3f(3f, 8f, 3f),
                    0.03f,
                    1f,
                    0.05f, 0.15f,
                    EnumParticleModel.Quad
                );

                smallSparks.MinPos.Set(Pos.X + 0.45, Pos.Y + 0.53, Pos.Z + 0.45);
                Api.World.SpawnParticles(smallSparks);*/
            }
        }
    }
}
