// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Text;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Input;

namespace osu.Game.API.UI.Scripting.Editor
{
    /// <summary>
    /// A lightweight, zero-dependency multi-line JavaScript code editor with syntax highlighting,
    /// selection and clipboard support. Focus/key handling is driven externally by
    /// <see cref="ScriptingConsoleOverlay"/>, which forwards key presses and text input.
    /// </summary>
    public partial class ScriptEditor : CompositeDrawable
    {
        private const float padding = 8;

        /// <summary>Raised whenever the underlying text is modified by the user.</summary>
        public event Action? TextChanged;

        /// <summary>Raised whenever the caret or selection changes.</summary>
        public event Action? CaretChanged;

        private readonly ScriptDocument document = new ScriptDocument();
        private readonly ISyntaxHighlighter highlighter = new ColorCodeHighlighter();
        private readonly FontUsage font = OsuFont.GetFont(size: 15);

        private readonly Dictionary<char, float> advanceCache = new Dictionary<char, float>();
        private readonly List<float[]> linePrefixes = new List<float[]>();

        private TextBuilder? measurer;

        private float lineHeight;
        private float maxLineWidth;

        private bool tokensDirty = true;
        private bool layoutDirty = true;
        private bool selectionDirty = true;
        private bool caretDirty = true;
        private bool scrollToCaret;
        private bool selecting;

        private OsuSpriteText probe = null!;
        private OsuScrollContainer verticalScroll = null!;
        private OsuScrollContainer horizontalScroll = null!;
        private Container content = null!;
        private Container selectionLayer = null!;
        private Container tokenLayer = null!;
        private Box caret = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private FontStore fontStore { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public ScriptEditor()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6,
                },
                verticalScroll = new OsuScrollContainer(Direction.Vertical)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = horizontalScroll = new OsuScrollContainer(Direction.Horizontal)
                    {
                        RelativeSizeAxes = Axes.X,
                        ScrollbarVisible = false,
                        Child = content = new Container
                        {
                            Children = new Drawable[]
                            {
                                selectionLayer = new Container { RelativeSizeAxes = Axes.Both },
                                tokenLayer = new Container { RelativeSizeAxes = Axes.Both },
                                caret = new Box
                                {
                                    Size = new Vector2(1.5f, 16),
                                    Colour = ScriptingColours.Caret,
                                    Origin = Anchor.TopLeft,
                                },
                            },
                        },
                    },
                },
                probe = new OsuSpriteText
                {
                    Text = "M",
                    Font = font,
                    Shadow = false,
                    Alpha = 0,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            caret.Loop(b => b.FadeTo(0, 530).FadeTo(1, 530));
        }

        public string Text
        {
            get => document.Text;
            set
            {
                document.SetText(value);
                onEdited();
            }
        }

        public bool CanUndo => document.CanUndo;

        public bool CanRedo => document.CanRedo;

        public int CaretLine => document.CaretLine;

        public int CaretColumn => document.CaretColumn;

        public void Undo()
        {
            document.Undo();
            onEdited();
        }

        public void Redo()
        {
            document.Redo();
            onEdited();
        }

        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            document.Insert(text);
            onEdited();
        }

        public void SelectAll()
        {
            document.SelectAll();
            onCaretMoved();
        }

        /// <summary>
        /// Attempts to handle a key press. Returns <c>true</c> if the key was consumed.
        /// Printable characters are intentionally not handled here and arrive via <see cref="InsertText"/>.
        /// </summary>
        public bool HandleKeyDown(KeyDownEvent e)
        {
            bool extend = e.ShiftPressed;
            bool control = e.ControlPressed;

            switch (e.Key)
            {
                case Key.Left:
                    document.MoveLeft(extend);
                    onCaretMoved();
                    return true;

                case Key.Right:
                    document.MoveRight(extend);
                    onCaretMoved();
                    return true;

                case Key.Up:
                    document.MoveUp(extend);
                    onCaretMoved();
                    return true;

                case Key.Down:
                    document.MoveDown(extend);
                    onCaretMoved();
                    return true;

                case Key.Home when control:
                    document.MoveToDocumentStart(extend);
                    onCaretMoved();
                    return true;

                case Key.Home:
                    document.MoveToLineStart(extend);
                    onCaretMoved();
                    return true;

                case Key.End when control:
                    document.MoveToDocumentEnd(extend);
                    onCaretMoved();
                    return true;

                case Key.End:
                    document.MoveToLineEnd(extend);
                    onCaretMoved();
                    return true;

                case Key.PageUp:
                    document.MoveUp(extend, linesPerPage());
                    onCaretMoved();
                    return true;

                case Key.PageDown:
                    document.MoveDown(extend, linesPerPage());
                    onCaretMoved();
                    return true;

                case Key.BackSpace:
                    document.Backspace();
                    onEdited();
                    return true;

                case Key.Delete:
                    document.DeleteForward();
                    onEdited();
                    return true;

                case Key.Enter:
                case Key.KeypadEnter:
                    document.InsertLineBreak();
                    onEdited();
                    return true;

                case Key.Tab:
                    document.Indent(extend);
                    onEdited();
                    return true;

                case Key.A when control:
                    document.SelectAll();
                    onCaretMoved();
                    return true;

                case Key.Z when control:
                    if (extend)
                        document.Redo();
                    else
                        document.Undo();

                    onEdited();
                    return true;

                case Key.Y when control:
                    document.Redo();
                    onEdited();
                    return true;

                case Key.C when control:
                    copySelection();
                    return true;

                case Key.X when control:
                    cutSelection();
                    return true;

                case Key.V when control:
                    pasteClipboard();
                    return true;

                default:
                    return false;
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (lineHeight <= 0 || linePrefixes.Count == 0)
                return false;

            updateCaretFromLocal(content.ToLocalSpace(e.ScreenSpaceMousePosition), false);
            selecting = true;
            return true;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!selecting || lineHeight <= 0)
                return false;

            updateCaretFromLocal(content.ToLocalSpace(e.ScreenSpaceMousePosition), true);
            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            selecting = false;
        }

        protected override void Update()
        {
            base.Update();

            if (lineHeight <= 0 && probe.DrawHeight > 0)
            {
                lineHeight = probe.DrawHeight * 1.4f;

                layoutDirty = true;
                tokensDirty = true;
                selectionDirty = true;
                caretDirty = true;
            }

            if (lineHeight <= 0)
                return;

            if (layoutDirty)
            {
                layoutContent();
                layoutDirty = false;
            }

            if (tokensDirty)
            {
                rebuildTokens();
                tokensDirty = false;
            }

            if (selectionDirty)
            {
                rebuildSelection();
                selectionDirty = false;
            }

            if (caretDirty)
            {
                updateCaretVisual();
                caretDirty = false;
            }
        }

        private void onEdited()
        {
            layoutDirty = true;
            tokensDirty = true;
            selectionDirty = true;
            caretDirty = true;
            scrollToCaret = true;

            TextChanged?.Invoke();
            CaretChanged?.Invoke();
        }

        private void onCaretMoved()
        {
            selectionDirty = true;
            caretDirty = true;
            scrollToCaret = true;

            CaretChanged?.Invoke();
        }

        /// <summary>
        /// Measures the advance (horizontal space) of a single character using the real font metrics,
        /// so that text can be laid out with a proportional font without the huge gaps produced by
        /// the framework's fixed-width mode.
        /// </summary>
        private float advanceOf(char character)
        {
            if (advanceCache.TryGetValue(character, out float cached))
                return cached;

            measurer ??= new TextBuilder(fontStore, font);
            measurer.Reset();
            measurer.AddCharacter(character);

            float advance = measurer.Characters.Count > 0 ? measurer.Characters[measurer.Characters.Count - 1].XAdvance : 0;
            advanceCache[character] = advance;
            return advance;
        }

        private void rebuildLinePrefixes()
        {
            linePrefixes.Clear();
            maxLineWidth = 0;

            IReadOnlyList<string> lines = document.Lines;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                float[] prefix = new float[line.Length + 1];
                float x = 0;

                for (int c = 0; c < line.Length; c++)
                {
                    prefix[c] = x;
                    x += advanceOf(line[c]);
                }

                prefix[line.Length] = x;
                linePrefixes.Add(prefix);
                maxLineWidth = Math.Max(maxLineWidth, x);
            }
        }

        private void layoutContent()
        {
            rebuildLinePrefixes();

            float width = Math.Max(maxLineWidth, horizontalScroll.DrawWidth) + padding * 2;
            float height = document.Lines.Count * lineHeight + padding * 2;

            content.Size = new Vector2(width, height);
            horizontalScroll.Height = height;
        }

        private void rebuildTokens()
        {
            tokenLayer.Clear();

            JsTokenType[] types = highlighter.Tokenize(document.Text);
            IReadOnlyList<string> lines = document.Lines;

            int index = 0;

            for (int line = 0; line < lines.Count && line < linePrefixes.Count; line++)
            {
                string text = lines[line];
                float[] prefix = linePrefixes[line];
                int column = 0;

                while (column < text.Length)
                {
                    JsTokenType type = types[index + column];
                    int start = column;

                    while (column < text.Length && types[index + column] == type)
                        column++;

                    tokenLayer.Add(new OsuSpriteText
                    {
                        Text = text.Substring(start, column - start),
                        Font = font,
                        Shadow = false,
                        Colour = ScriptingColours.ForToken(type),
                        Position = new Vector2(padding + prefix[start], padding + line * lineHeight),
                        Origin = Anchor.TopLeft,
                    });
                }

                index += text.Length + 1;
            }
        }

        private void rebuildSelection()
        {
            selectionLayer.Clear();

            foreach ((int line, int start, int end) in document.GetSelectionRanges())
            {
                if (end <= start || line >= linePrefixes.Count)
                    continue;

                float[] prefix = linePrefixes[line];

                selectionLayer.Add(new Box
                {
                    Position = new Vector2(padding + prefix[start], padding + line * lineHeight),
                    Size = new Vector2(prefix[end] - prefix[start], lineHeight),
                    Colour = ScriptingColours.Selection,
                });
            }
        }

        private void updateCaretVisual()
        {
            if (document.CaretLine >= linePrefixes.Count)
                return;

            caret.Position = new Vector2(padding + linePrefixes[document.CaretLine][document.CaretColumn], padding + document.CaretLine * lineHeight);
            caret.Height = lineHeight;

            if (scrollToCaret)
            {
                ensureCaretVisible();
                scrollToCaret = false;
            }
        }

        private void ensureCaretVisible()
        {
            float caretX = padding + linePrefixes[document.CaretLine][document.CaretColumn];
            float caretY = padding + document.CaretLine * lineHeight;

            double top = verticalScroll.Current;

            if (caretY < top)
                verticalScroll.ScrollTo(Math.Max(0, caretY - lineHeight), false);
            else if (caretY + lineHeight * 2 > top + verticalScroll.DrawHeight)
                verticalScroll.ScrollTo(caretY + lineHeight * 2 - verticalScroll.DrawHeight, false);

            double left = horizontalScroll.Current;
            float caretWidth = advanceOf('M');

            if (caretX < left + caretWidth * 4)
                horizontalScroll.ScrollTo(Math.Max(0, caretX - caretWidth * 4), false);
            else if (caretX + caretWidth * 4 > left + horizontalScroll.DrawWidth)
                horizontalScroll.ScrollTo(caretX + caretWidth * 4 - horizontalScroll.DrawWidth, false);
        }

        private void updateCaretFromLocal(Vector2 local, bool extend)
        {
            int line = (int)Math.Floor((local.Y - padding) / Math.Max(lineHeight, 1));
            line = Math.Clamp(line, 0, document.LineCount - 1);

            float[] prefix = linePrefixes[line];
            float target = local.X - padding;

            int column = 0;
            float best = float.MaxValue;

            for (int i = 0; i < prefix.Length; i++)
            {
                float distance = Math.Abs(prefix[i] - target);

                if (distance < best)
                {
                    best = distance;
                    column = i;
                }
            }

            document.SetCaret(line, column, extend);
            onCaretMoved();
        }

        private int linesPerPage()
        {
            if (lineHeight <= 0)
                return 1;

            return Math.Max(1, (int)(verticalScroll.DrawHeight / lineHeight) - 1);
        }

        private void copySelection()
        {
            if (document.HasSelection)
                clipboard.SetText(document.GetSelectedText());
        }

        private void cutSelection()
        {
            if (!document.HasSelection)
                return;

            clipboard.SetText(document.GetSelectedText());
            document.Backspace();
            onEdited();
        }

        private void pasteClipboard()
        {
            string? text = clipboard.GetText();

            if (string.IsNullOrEmpty(text))
                return;

            document.Insert(text);
            onEdited();
        }
    }
}
