using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Rustwall.Utils
{
    internal class GuiDialogPunchCard : GuiDialogGeneric
    {
        //public string AllPagesText;

        public string Title;

        //protected int curPage;

        protected int maxLines = 20;

        protected int maxWidth = 1800;

        public List<PagePosition> Pages = new List<PagePosition>();

        protected CairoFont font = CairoFont.TextInput().WithFontSize(18f);

        private TranscribePressedDelegate onTranscribedPressed;

        protected bool KeyboardNavigation = true;

        /*public string CurPageText
        {
            get
            {
                if (curPage >= Pages.Count)
                {
                    return "";
                }

                if (Pages[curPage].Start < AllPagesText.Length)
                {
                    return AllPagesText.Substring(Pages[curPage].Start, Math.Min(AllPagesText.Length - Pages[curPage].Start, Pages[curPage].Length)).TrimStart(' ');
                }

                return "";
            }
        }*/

        public string Text;

        public double textAreaWidth => GuiElement.scaled(maxWidth);
        internal GuiDialogPunchCard(ItemStack bookStack, ICoreClientAPI capi, TranscribePressedDelegate onTranscribedPressed = null) 
            : base("", capi)
        {
            this.onTranscribedPressed = onTranscribedPressed;

            Text = bookStack.Attributes.GetString("text", "").Replace("\r", "");
            Title = "MBI 8501 PUNCH CARD";

            if (OperatingSystem.IsWindows())
            {
                monoFont = CairoFont.TextInput().WithFontSize(18f).WithFont("Courier New");
            }
            if (OperatingSystem.IsLinux())
            {
                monoFont = CairoFont.TextInput().WithFontSize(18f).WithFont("DejaVu Sans Mono");
            }
            if (OperatingSystem.IsIOS())
            {
                monoFont = CairoFont.TextInput().WithFontSize(18f).WithFont("Menlo");
            }

            //Text = "&-0123456789ABCDEFGHIJKLMNOPQR/STUVWXYZ:#@'=\"¢.<(+|!$*);¬ ,%_>?";

            this.Compose();
        }
        
        protected CairoFont monoFont;
        protected void Compose()
        {
            double num = monoFont.GetFontExtents().Height * monoFont.LineHeightMultiplier / (double)RuntimeEnv.GUIScale;
            ElementBounds elementBounds = ElementBounds.Fixed(0.0, 30.0, maxWidth, (double)(maxLines + ((Pages.Count > 1) ? 2 : 0)) * num + 1.0);
            ElementBounds elementBounds2 = ElementBounds.FixedSize(60.0, 30.0).FixedUnder(elementBounds, 23.0).WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(10.0, 2.0);
            ElementBounds bounds = ElementBounds.FixedSize(80.0, 30.0).FixedUnder(elementBounds, 33.0).WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPadding(10.0, 2.0);
            ElementBounds elementBounds3 = ElementBounds.FixedSize(60.0, 30.0).FixedUnder(elementBounds, 23.0).WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(10.0, 2.0);
            ElementBounds elementBounds4 = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(elementBounds2, 25.0).WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(10.0, 2.0);
            ElementBounds bounds2 = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(elementBounds3, 25.0).WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(10.0, 2.0);
            ElementBounds elementBounds5 = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            elementBounds5.BothSizing = ElementSizing.FitToChildren;
            elementBounds5.WithChildren(elementBounds4);
            ElementBounds bounds3 = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(0.0 - GuiStyle.DialogToScreenPadding, 0.0);
            base.SingleComposer = capi.Gui.CreateCompo("blockentitytexteditordialog", bounds3).AddShadedDialogBG(elementBounds5).AddDialogTitleBar(Title, delegate
            {
                TryClose();
            })
                .BeginChildElements(elementBounds5)
                .AddRichtext("", monoFont, elementBounds, "text")
                /*.AddIf(Pages.Count > 1)
                .AddSmallButton(Lang.Get("<"), prevPage, elementBounds2)
                .EndIf()
                .AddDynamicText("1/1", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), bounds, "pageNum")
                .AddIf(Pages.Count > 1)
                .AddSmallButton(Lang.Get(">"), nextPage, elementBounds3)
                .EndIf()*/
                //.AddSmallButton(Lang.Get("Close"), () => TryClose(), elementBounds4)
                .AddIf(onTranscribedPressed != null)
                .AddSmallButton(Lang.Get("Transcribe"), onButtonTranscribe, bounds2)
                .EndIf()
                .EndChildElements()
                .Compose();
            updatePage();
        }

        private bool onButtonTranscribe()
        {
            onTranscribedPressed(Text, Title, 1);
            return true;
        }

        protected void updatePage(bool setCaretPosToEnd = true)
        {
            string curPageText = Text;

            GuiElement element = base.SingleComposer.GetElement("text");
            if (element is GuiElementTextArea guiElementTextArea)
            {
                guiElementTextArea.SetValue(curPageText, setCaretPosToEnd);
            }
            else
            {
                (element as GuiElementRichtext).SetNewText(curPageText, monoFont);
            }
        }
    }
}
