using Microsoft.VisualBasic;
using Rustwall.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace Rustwall.RWItem
{
    internal class ItemPunchCard : ItemBook 
    {
        private ModSystemEditableBook bookModSys;

        private int maxPageCount;

        private bool editable;

        private ICoreClientAPI capi;

        private WorldInteraction[] interactions;

        private ItemSlot curSlot;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
            editable = Attributes["editable"].AsBool();
            maxPageCount = Attributes["maxPageCount"].AsInt(90);
            bookModSys = api.ModLoader.GetModSystem<ModSystemEditableBook>();
            interactions = ObjectCacheUtil.GetOrCreate(api, "bookInteractions", delegate
            {
                List<ItemStack> list = new List<ItemStack>();
                foreach (CollectibleObject collectible in api.World.Collectibles)
                {
                    if (collectible.Attributes != null && collectible.Attributes.IsTrue("writingTool"))
                    {
                        list.Add(new ItemStack(collectible));
                    }
                }

                return new WorldInteraction[2]
                {
                new WorldInteraction
                {
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = "heldhelp-read",
                    ShouldApply = delegate
                    {
                        ItemSlot activeHotbarSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
                        ITreeAttribute treeAttribute = activeHotbarSlot.Itemstack?.Attributes;
                        return isReadable(activeHotbarSlot) && treeAttribute != null && (treeAttribute.HasAttribute("text") || treeAttribute.HasAttribute("textCodes"));
                    }
                },
                new WorldInteraction
                {
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = list.ToArray(),
                    ActionLangCode = "heldhelp-write",
                    GetMatchingStacks = (WorldInteraction wi, BlockSelection bs, EntitySelection es) => (capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.Attributes.GetString("signedby") == null) ? wi.Itemstacks : null
                }
                };
            });
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (!isReadable(slot))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            IPlayer player = (byEntity as EntityPlayer).Player;
            /*if (editable && isWritingTool(byEntity.LeftHandItemSlot) && !isSigned(slot))
            {
                bookModSys.BeginEdit(player, slot);
                if (api.Side == EnumAppSide.Client)
                {
                    GuiDialogEditableBook dlg = new GuiDialogEditableBook(slot.Itemstack, api as ICoreClientAPI, maxPageCount);
                    dlg.OnClosed += delegate
                    {
                        if (dlg.DidSave)
                        {
                            bookModSys.EndEdit(player, dlg.AllPagesText, dlg.Title, dlg.DidSign);
                        }
                        else
                        {
                            bookModSys.CancelEdit(player);
                        }
                    };
                    dlg.TryOpen();
                }

                handling = EnumHandHandling.PreventDefault;
            }
            else*/ 
            
            if (slot.Itemstack.Attributes.HasAttribute("text") || slot.Itemstack.Attributes.HasAttribute("textCodes"))
            {
                bookModSys.BeginEdit(player, slot);
                if (api.Side == EnumAppSide.Client)
                {
                    curSlot = slot;
                    GuiDialogPunchcard guiDialogPunchCard = new GuiDialogPunchcard(slot.Itemstack, api as ICoreClientAPI, onTranscribePressed);
                    guiDialogPunchCard.OnClosed += delegate
                    {
                        curSlot = null;
                        bookModSys.CancelEdit(player);
                    };
                    guiDialogPunchCard.TryOpen();
                }

                handling = EnumHandHandling.PreventDefault;
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
        }

        private void onTranscribePressed(string pageText, string pageTitle, int pageNumber)
        {
            bookModSys.Transcribe(capi.World.Player, pageText, pageTitle, pageNumber, curSlot);
        }
    }
}
