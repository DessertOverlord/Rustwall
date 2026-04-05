using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Rustwall.RWBlockEntity.RustwallMachinery
{
    public class BlockEntityPumpUnit : BlockEntityRebuildable
    {
        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        protected override void InitAnimations(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client)
            {
                float rotY = Block.Shape.rotateY;
                animUtil?.InitializeAnimator("rebuildableblock-pumpunit", null, null, new Vec3f(0, rotY, 0));
            }
        }

        protected override void ActivateAnimations()
        {
            animUtil?.StartAnimation(new AnimationMetaData() { Animation = "active", Code = "active", EaseInSpeed = 1, EaseOutSpeed = 1, AnimationSpeed = 0.5f });
            MarkDirty(true);
        }

        protected override void DeactivateAnimations()
        {
            animUtil?.StopAnimation("active");
            MarkDirty(true);
        }

    }
}
