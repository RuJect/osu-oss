// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.API.Scripting;
using osu.Game.API.UI.Scripting.Components;
using osu.Game.API.UI.Scripting.Editor;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using osu.Game.API.Services;

namespace osu.Game.API.UI.Scripting
{
    /// <summary>
    /// The osu!-styled scripting console overlay: a script browser, a JavaScript editor and an output console.
    /// Toggled via <c>Ctrl+F4</c> while the game is running.
    /// </summary>
    public partial class ScriptingConsoleOverlay : FullscreenOverlay<ScriptingOverlayHeader>
    {
        private const string default_script = "// New script\nosu.log(\"hello from a new script\");\n";

        private readonly ScriptRepository repository;
        private readonly ScriptRunner runner;
        private readonly OutputLog outputLog;

        private ScriptListPanel scriptList = null!;
        private RunningScriptsPanel runningPanel = null!;
        private ScriptEditor editor = null!;
        private ScriptOutputConsole outputConsole = null!;

        private Container body = null!;
        private OsuSpriteText statusText = null!;

        private ScriptDefinition? current;
        private bool textInputActive;

        [Resolved]
        private TextInputSource textInput { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public ScriptingConsoleOverlay(ScriptManager manager)
            : base(OverlayColourScheme.Plum)
        {
            repository = manager.Repository;
            runner = manager.Runner;
            outputLog = manager.Output;
        }

        protected override ScriptingOverlayHeader CreateHeader() => new ScriptingOverlayHeader();

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Depth = float.MinValue;

            var toolbar = new FillFlowContainer
            {
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8),
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            };

            toolbar.AddRange(new Drawable[]
            {
                createButton("New", FontAwesome.Solid.Plus, colours.Blue1, colours.Blue2, 84, createNew),
                createButton("Save", FontAwesome.Solid.Save, colours.Green1, colours.Green2, 90, save),
                createButton("Save As", FontAwesome.Solid.File, colours.Lime1, colours.Lime2, 108, saveAs),
                createButton("Run", FontAwesome.Solid.Play, colours.Pink1, colours.Pink2, 84, run),
                createButton("Reload", FontAwesome.Solid.Sync, colours.Orange1, colours.Orange2, 104, reload),
            });

            var toolbarRow = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 48,
                Children = new Drawable[]
                {
                    toolbar,
                    statusText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Font = OsuFont.GetFont(size: 13),
                        Colour = Colour4.FromHex("B0B0BE"),
                        Shadow = false,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Colour = ColourProvider.Background6,
                    },
                },
            };

            var columns = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Top = 56 },
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 280,
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(1, 0.54f),
                                Child = scriptList = new ScriptListPanel(repository, runner),
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(1, 0.44f),
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Child = runningPanel = new RunningScriptsPanel(runner),
                            },
                        },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 292 },
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(1, 0.74f),
                                Child = editor = new ScriptEditor
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(1, 0.24f),
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Child = outputConsole = new ScriptOutputConsole(outputLog),
                            },
                        },
                    },
                },
            };

            body = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    toolbarRow,
                    columns,
                },
            };

            Content.Children = new Drawable[]
            {
                Header,
                body,
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            body.Padding = new MarginPadding
            {
                Top = Header.DrawHeight,
                Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING,
                Bottom = 20,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scriptList.ScriptSelected += select;
            runningPanel.StopRequested += instance => runner.Stop(instance);
            runningPanel.SelectedSourceChanged += source => outputConsole.SetFilter(source);

            editor.TextChanged += onEditorTextChanged;
            editor.CaretChanged += updateStatus;

            updateStatus();

            Schedule(() =>
            {
                if (current == null && repository.Definitions.Count > 0)
                    select(repository.Definitions[0]);
            });
        }

        protected override void PopIn()
        {
            base.PopIn();

            if (textInputActive)
                return;

            textInputActive = true;
            textInput.OnTextInput += onTextInput;
            textInput.Activate(new TextInputProperties(TextInputType.Code), RectangleF.Empty);
        }

        protected override void PopOutComplete()
        {
            base.PopOutComplete();

            if (!textInputActive)
                return;

            textInputActive = false;
            textInput.OnTextInput -= onTextInput;
            textInput.Deactivate();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.ControlPressed && (e.Key == Key.Enter || e.Key == Key.KeypadEnter))
            {
                run();
                return true;
            }

            if (editor.HandleKeyDown(e))
                return true;

            return base.OnKeyDown(e);
        }

        private void onTextInput(string text) => editor.InsertText(text);

        private void select(ScriptDefinition definition)
        {
            current = definition;
            editor.Text = definition.Source;
            scriptList.SetSelected(definition);
            updateStatus();
        }

        private void createNew()
        {
            ScriptDefinition definition = repository.CreateAnonymous(default_script);
            select(definition);
        }

        private void save()
        {
            if (current == null || !current.IsFile)
            {
                saveAs();
                return;
            }

            current.Source = editor.Text;
            repository.Save(current);
        }

        private void saveAs()
        {
            if (current == null)
                return;

            if (current.IsFile)
            {
                save();
                return;
            }

            current.Source = editor.Text;
            string? fileName = repository.SaveAsFile(current);

            if (fileName == null)
                return;

            repository.Remove(current);

            ScriptDefinition? created = repository.Definitions.FirstOrDefault(d => d.FileName == fileName);

            if (created != null)
                select(created);
        }

        private void run()
        {
            if (current == null)
                current = repository.CreateAnonymous(string.Empty);

            current.Source = editor.Text;
            runner.Run(current);
            updateStatus();
        }

        private void reload()
        {
            if (current == null || !current.IsFile)
                return;

            repository.Reload(current);
            editor.Text = current.Source;
            updateStatus();
        }

        private void onEditorTextChanged()
        {
            if (current != null)
                current.Source = editor.Text;
        }

        private void updateStatus()
        {
            string name = current?.Name ?? "<no script selected>";
            statusText.Text = $"{name}    Ln {editor.CaretLine + 1}, Col {editor.CaretColumn + 1}";
        }

        private static ShearedIconButton createButton(string text, IconUsage icon, Color4 darker, Color4 lighter, float width, Action action) => new ShearedIconButton(icon, text)
        {
            Width = width,
            Height = 34,
            DarkerColour = darker,
            LighterColour = lighter,
            Action = action,
        };
    }
}
