using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Rustwall.Utils
{
    internal class GuiDialogPunchcard : GuiDialogReadonlyBook
    {
        private TranscribePressedDelegate onTranscribedPressed;

        internal GuiDialogPunchcard(ItemStack bookStack, ICoreClientAPI capi, TranscribePressedDelegate onTranscribedPressed = null) 
            : base(bookStack, capi, onTranscribedPressed)
        {
            this.onTranscribedPressed = onTranscribedPressed;
            if (bookStack.Attributes.HasAttribute("textCodes"))
            {
                AllPagesText = string.Join("\n", (bookStack.Attributes["textCodes"] as StringArrayAttribute).value.Select((string code) => Lang.Get(code))).Replace("\r", "").Replace("___NEWPAGE___", "");
                Title = Lang.Get(bookStack.Attributes.GetString("titleCode", ""));
            }
            else
            {
                AllPagesText = bookStack.Attributes.GetString("text", "").Replace("\r", "");
                Title = bookStack.Attributes.GetString("title", "");
            }

            //monoFont.Fontname = "Courier New";
            Pages = this.Pageize(AllPagesText, monoFont, textAreaWidth, maxLines);
            this.Compose();
        }

        protected CairoFont monoFont = new CairoFont(22f, "Courier New");

        protected new List<PagePosition> Pageize(string fullText, CairoFont font, double pageWidth, int maxLinesPerPage)
        {
            TextDrawUtil textDrawUtil = new();
            Stack<string> stack = new Stack<string>();
            var newSpan = textDrawUtil.Lineize(font, fullText, pageWidth, EnumLinebreakBehavior.Default, keepLinebreakChar: true);
            newSpan.Reverse();
            foreach (TextLine item in newSpan)
            {
                stack.Push(item.Text);
            }

            List<PagePosition> list = new List<PagePosition>();
            int num = 0;
            int num2 = 0;
            while (stack.Count > 0)
            {
                int num3 = 0;
                while (num3 < maxLinesPerPage && stack.Count > 0)
                {
                    string text = stack.Pop();
                    num3++;
                    num2 += text.Length;
                }

                if (num3 > 0)
                {
                    list.Add(new PagePosition
                    {
                        Start = num,
                        Length = num2,
                        LineCount = num3
                    });
                    num += num2;
                }

                num2 = 0;
            }

            if (list.Count == 0)
            {
                list.Add(new PagePosition
                {
                    Start = 0,
                    Length = 0
                });
            }

            return list;
        }
       
        protected override void Compose()
        {
            //monoFont.Fontname = "monospace";
            //monoFont.Fontname = "Courier New";
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
                .AddIf(Pages.Count > 1)
                .AddSmallButton(Lang.Get("<"), prevPage, elementBounds2)
                .EndIf()
                .AddDynamicText("1/1", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), bounds, "pageNum")
                .AddIf(Pages.Count > 1)
                .AddSmallButton(Lang.Get(">"), nextPage, elementBounds3)
                .EndIf()
                .AddSmallButton(Lang.Get("Close"), () => TryClose(), elementBounds4)
                .AddIf(onTranscribedPressed != null)
                .AddSmallButton(Lang.Get("Transcribe"), onButtonTranscribe, bounds2)
                .EndIf()
                .EndChildElements()
                .Compose();
            updatePage();





            //base.Compose();
        }

        private bool onButtonTranscribe()
        {
            onTranscribedPressed(CurPageText, Title, curPage);
            return true;
        }

        private bool prevPage()
        {
            curPage = Math.Max(curPage - 1, 0);
            updatePage();
            return true;
        }



    }
}
