// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Platform;
using osu.Framework.Utils;

namespace osu.Game.Auth
{
    /// <summary>
    /// Tracks auth module startup across runs so a crashed previous run can
    /// invalidate a possibly corrupt native module. Clean-room port.
    /// </summary>
    internal sealed class StartupState
    {
        public const string LOAD_SUCCESS = "auth loaded";

        private const string log_file = ".auth_startup";

        private readonly Storage storage;
        private string? previous;

        public StartupState(Storage storage)
        {
            this.storage = storage;

            try
            {
                if (storage.Exists(log_file))
                {
                    General.AttemptWithRetryOnException<IOException>(readPrevious);
                    General.AttemptWithRetryOnException<IOException>(deletePrevious);
                }

                Add("log started");
            }
            catch
            {
                // ignored.
            }
        }

        public bool HasPreviousStartup() => previous != null;

        public bool PreviousStartupContains(string text) => previous?.Contains(text) ?? false;

        public void Add(string text)
        {
            try
            {
                General.AttemptWithRetryOnException<IOException>(() =>
                {
                    using var writer = new StreamWriter(storage.GetStream(log_file, FileAccess.Write, FileMode.Append));
                    writer.WriteLine(text);
                });
            }
            catch
            {
                // ignored.
            }
        }

        private void readPrevious()
        {
            using var reader = new StreamReader(storage.GetStream(log_file));
            previous = reader.ReadToEnd();
        }

        private void deletePrevious() => storage.Delete(log_file);
    }
}
