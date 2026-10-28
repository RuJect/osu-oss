// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.Versioning;
using osu.Framework.Logging;

namespace osu.Desktop.Windows
{
    [SupportedOSPlatform("windows")]
    public static class SharedShortcutManager
    {
        private const string shortcut_name = "osu!oss (shared).lnk";
        private const string arguments = "--shared";

        public static void Install()
        {
            try
            {
                string? executable = Environment.ProcessPath;

                if (string.IsNullOrEmpty(executable))
                    return;

                string workingDirectory = Path.GetDirectoryName(executable)!;
                string icon = Icons.OsuOss;

                foreach (string directory in shortcutDirectories())
                    createShortcut(Path.Combine(directory, shortcut_name), executable, workingDirectory, icon);

                Logger.Log("Installed shared-data shortcut.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to install shared-data shortcut.");
            }
        }

        public static void Uninstall()
        {
            try
            {
                foreach (string directory in shortcutDirectories())
                {
                    string path = Path.Combine(directory, shortcut_name);

                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to remove shared-data shortcut.");
            }
        }

        private static string[] shortcutDirectories()
        {
            string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs");

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            return new[] { startMenu, desktop };
        }

        private static void createShortcut(string path, string executable, string workingDirectory, string icon)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");

            if (shellType == null)
                throw new InvalidOperationException("WScript.Shell is unavailable.");

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(path);

            shortcut.TargetPath = executable;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = icon;
            shortcut.Description = "osu!oss using osu!lazer data";
            shortcut.Save();
        }
    }
}
