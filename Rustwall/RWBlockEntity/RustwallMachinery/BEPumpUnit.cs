using Rustwall.RWBlockEntity.BERebuildable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
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
                animUtil?.InitializeAnimator("rebuildableblock-pumpunit");
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
