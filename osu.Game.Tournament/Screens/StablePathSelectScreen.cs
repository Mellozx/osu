// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Tournament.Models;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Components;
using osuTK;

namespace osu.Game.Tournament.Screens
{
    public class StablePathSelectScreen : TournamentScreen
    {
        private DirectorySelector directorySelector;

        [Resolved]
        private StableInfo stableInfo { get; set; }

        [Resolved]
        private MatchIPCInfo ipc { get; set; }

        private DialogOverlay overlay;

        [Resolved(canBeNull: true)]
        private TournamentSceneManager sceneManager { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(Storage storage, OsuColour colours)
        {
            // begin selection in the parent directory of the current storage location
            var initialPath = new DirectoryInfo(storage.GetFullPath(string.Empty)).Parent?.FullName;

            if (!string.IsNullOrEmpty(stableInfo.StablePath.Value))
            {
                // If the original path info for osu! stable is not empty, set it to the parent directory of that location
                initialPath = new DirectoryInfo(stableInfo.StablePath.Value).Parent?.FullName;
            }

            AddRangeInternal(new Drawable[]
            {
                new Container
                {
                    Masking = true,
                    CornerRadius = 10,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(0.5f, 0.8f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colours.GreySeafoamDark,
                            RelativeSizeAxes = Axes.Both,
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.Relative, 0.8f),
                                new Dimension(),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = "Please select a new location",
                                        Font = OsuFont.Default.With(size: 40)
                                    },
                                },
                                new Drawable[]
                                {
                                    directorySelector = new DirectorySelector(initialPath)
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    }
                                },
                                new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(20),
                                        Children = new Drawable[]
                                        {
                                            new TriangleButton
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Width = 300,
                                                Text = "Select stable path",
                                                Action = () => changePath(storage)
                                            },
                                            new TriangleButton
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Width = 300,
                                                Text = "Auto detect",
                                                Action = autoDetect
                                            },
                                        }
                                    }
                                }
                            }
                        }
                    },
                },
                new BackButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    State = { Value = Visibility.Visible },
                    Action = () => sceneManager?.SetScreen(typeof(SetupScreen))
                }
            });
        }

        private void changePath(Storage storage)
        {
            var target = directorySelector.CurrentDirectory.Value.FullName;
            var fileBasedIpc = ipc as FileBasedIPC;
            Logger.Log($"Changing Stable CE location to {target}");

            if (!fileBasedIpc.checkExists(target))
            {
                overlay = new DialogOverlay();
                overlay.Push(new IPCErrorDialog("This is an invalid IPC Directory", "Select a directory that contains an osu! stable cutting edge installation and make sure it has an empty ipc.txt file in it."));
                AddInternal(overlay);
                Logger.Log("Folder is not an osu! stable CE directory");
                return;
                // Return an error in the picker that the directory does not contain ipc.txt
            }

            stableInfo.StablePath.Value = target;

            try
            {
                using (var stream = storage.GetStream(StableInfo.STABLE_CONFIG, FileAccess.Write, FileMode.Create))
                using (var sw = new StreamWriter(stream))
                {
                    sw.Write(JsonConvert.SerializeObject(stableInfo,
                        new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore,
                            DefaultValueHandling = DefaultValueHandling.Ignore,
                        }));
                }

             
                fileBasedIpc?.LocateStableStorage();
                sceneManager?.SetScreen(typeof(SetupScreen));
            }
            catch (Exception e)
            {
                Logger.Log($"Error during migration: {e.Message}", level: LogLevel.Error);
            }
        }

        private void autoDetect()
        {
            stableInfo.StablePath.Value = string.Empty; // This forces findStablePath() to look elsewhere.
            var fileBasedIpc = ipc as FileBasedIPC;
            fileBasedIpc?.LocateStableStorage();

            if (fileBasedIpc?.IPCStorage == null)
            {
                // Could not auto detect
                overlay = new DialogOverlay();
                overlay.Push(new IPCErrorDialog("Failed to auto detect", "An osu! stable cutting-edge installation could not be auto detected.\nPlease try and manually point to the directory."));
                AddInternal(overlay);
            }
            else
            {
                sceneManager?.SetScreen(typeof(SetupScreen));
            }
        }
    }
}
