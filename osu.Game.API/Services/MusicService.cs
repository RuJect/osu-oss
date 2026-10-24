// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Overlays;

namespace osu.Game.API.Services
{
    /// <summary>Metadata for the currently playing track, as exposed to scripts and plugins.</summary>
    public sealed record MusicTrack(string Title, string Artist, int BeatmapId, int SetId);

    /// <summary>
    /// Control of the global music playback. Operations are marshalled onto the update thread.
    /// </summary>
    public class MusicService
    {
        // shortcut: reaches into MusicController's private current beatmap; revisit if a public accessor appears.
        private static readonly FieldInfo current_field =
            typeof(MusicController).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly MusicController music;
        private readonly GameHost host;

        public MusicService(MusicController music, GameHost host)
        {
            this.music = music;
            this.host = host;
        }

        public bool IsPlaying => music.IsPlaying;

        public void Play() => schedule(() => music.Play(requestedByUser: true));

        public void Pause()
        {
            schedule(() =>
            {
                if (music.IsPlaying)
                    music.Stop(requestedByUser: true);
            });
        }

        public void TogglePause() => schedule(() => music.TogglePause());

        public void Stop() => schedule(() => music.Stop(requestedByUser: true));

        public void Next() => music.NextTrack();

        public void Previous() => music.PreviousTrack();

        public void Seek(double position) => music.SeekTo(position);

        /// <summary>Metadata for the currently loaded track, or <c>null</c> when none is loaded.</summary>
        public MusicTrack? CurrentTrack()
        {
            WorkingBeatmap? beatmap = current_field.GetValue(music) as WorkingBeatmap;

            if (beatmap == null)
                return null;

            IBeatmapInfo info = beatmap.BeatmapInfo;
            IBeatmapSetInfo set = beatmap.BeatmapSetInfo;

            return new MusicTrack(info.Metadata.Title, info.Metadata.Artist, info.OnlineID, set.OnlineID);
        }

        private void schedule(Action action) => host.UpdateThread.Scheduler.Add(action);
    }
}
