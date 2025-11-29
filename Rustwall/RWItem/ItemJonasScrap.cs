using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Rustwall.RWItem
{
    internal class ItemJonasScrap : Item
    {
        float curX;
        float curY;

        float prevSecUsed;
        List<JsonItemStack> possibleCraftResultStacks;

        LCGRandom rnd;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            rnd = new LCGRandom(api.World.Seed);

            List<JsonItemStack> jstacks = Attributes["output"].AsObject<List<JsonItemStack>>();

            possibleCraftResultStacks = [.. jstacks];
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (slot.Itemstack.TempAttributes.GetBool("consumed") == true) return;

            handling = EnumHandHandling.PreventDefault;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            byEntity.World.RegisterCallback((dt) =>
            {
                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                {
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/messycraft"), byPlayer, byPlayer);
                }
            }, 250);
        }
        
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float nowx = 0, nowy = 0;

                if (secondsUsed > 0.3f)
                {
                    int cnt = (int)(secondsUsed * 10);
                    rnd.InitPositionSeed(cnt, 0);

                    float targetx = 3f * (rnd.NextFloat() - 0.5f);
                    float targety = 1.5f * (rnd.NextFloat() - 0.5f);

                    float dt = secondsUsed - prevSecUsed;

                    nowx = (curX - targetx) * dt * 2;
                    nowy = (curY - targety) * dt * 2;
                }

                tf.Translation.Set(nowx - Math.Min(1.5f, secondsUsed * 4), nowy, 0);

                curX = nowx;
                curY = nowy;

                prevSecUsed = secondsUsed;
            }

            if (api.World.Side == EnumAppSide.Server) return true;

            return secondsUsed < 4.6f;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > 4.5f)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    List<JsonItemStack> outputJsonStacks = [.. possibleCraftResultStacks];

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    foreach (JsonItemStack jsonItemStack in outputJsonStacks)
                    { 
                        jsonItemStack.Resolve(api.World, "Scrap weapon kit craft result");
                        if (jsonItemStack.ResolvedItemstack is not null)
                        {
                            byEntity.World.SpawnItemEntity(jsonItemStack.ResolvedItemstack, byEntity.Pos.XYZ);
                        }
                    }
                }
                else
                {
                    //slot.Itemstack.TempAttributes.SetBool("consumed", true);
                }
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return [
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-craftscrapweapon",
                    MouseButton = EnumMouseButton.Right
                }
            ];
        }
    }
}