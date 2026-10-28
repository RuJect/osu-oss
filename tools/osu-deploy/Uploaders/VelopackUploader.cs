// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Linq;
using System.Net.Http;
using osu.Framework.IO.Network;

namespace osu.Desktop.Deploy.Uploaders
{
    public class VelopackUploader : Uploader
    {
        private readonly string applicationName;
        private readonly string operatingSystemName;
        private readonly string runtimeIdentifier;
        private readonly string channel;
        private readonly string? extraArgs;
        private readonly string stagingPath;

        public VelopackUploader(string applicationName, string operatingSystemName, string runtimeIdentifier, string channel, string? extraArgs = null, string? stagingPath = null)
        {
            this.applicationName = applicationName;
            this.operatingSystemName = operatingSystemName;
            this.runtimeIdentifier = runtimeIdentifier;
            this.channel = channel;
            this.extraArgs = extraArgs;
            this.stagingPath = stagingPath ?? Program.StagingPath;
        }

        public override void RestoreBuild()
        {
            if (Program.CanGitHub)
            {
                Program.RunCommand("dotnet", $"vpk download github"
                                             + $" --repoUrl=\"{Program.GitHubRepoUrl}\""
                                             + $" --token=\"{Program.GitHubAccessToken}\""
                                             + $" --channel=\"{channel}\""
                                             + $" --outputDir=\"{Program.ReleasesPath}\""
                                             + " --pre",
                    throwIfNonZero: false,
                    useSolutionPath: false);
            }
        }

        // influences the name of the .app bundle. but needs to be osu!(lazer) for windows shortcuts
        protected virtual string PackTitle => Program.PackTitle;

        public override void PublishBuild(string version)
        {
            Program.RunCommand("dotnet", $"vpk [{operatingSystemName}] pack"
                                         + $" --packTitle=\"{PackTitle}\""
                                         + $" --packAuthors=\"{Program.PackAuthors}\""
                                         + $" --packId=\"{Program.PackId}\""
                                         + $" --packVersion=\"{version}\""
                                         + $" --runtime=\"{runtimeIdentifier}\""
                                         + $" --outputDir=\"{Program.ReleasesPath}\""
                                         + $" --mainExe=\"{applicationName}\""
                                         + $" --packDir=\"{stagingPath}\""
                                         + $" --channel=\"{channel}\""
                                         + " --verbose"
                                         + $" {extraArgs}",
                useSolutionPath: false);

            if (Program.CanGitHub && Program.GitHubUpload)
            {
                Program.RunCommand("dotnet", $"vpk upload github"
                                             + $" --repoUrl=\"{Program.GitHubRepoUrl}\""
                                             + $" --token=\"{Program.GitHubAccessToken}\""
                                             + $" --outputDir=\"{Program.ReleasesPath}\""
                                             + $" --tag=\"{version}\""
                                             + $" --releaseName=\"{version}\""
                                             + " --publish true"
                                             + (Program.GitHubPreRelease ? " --pre true" : string.Empty)
                                             + $" --merge"
                                             + $" --channel=\"{channel}\"",
                    useSolutionPath: false);
            }
        }

        protected void RenameAsset(string fromName, string toName, string version)
        {
            if (!Program.CanGitHub || !Program.GitHubUpload)
                return;

            Logger.Write($"Renaming asset '{fromName}' to '{toName}'");

            GitHubAsset? asset = null;

            for (int attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    GitHubRelease release = Program.GetGithubRelease(version)
                                            ?? throw new Exception("Release not found.");

                    asset = release.Assets.SingleOrDefault(a => a.Name == fromName);

                    if (asset != null)
                        break;
                }
                catch (Exception e) when (attempt < 10)
                {
                    Logger.Write($"Release not ready for rename ({e.Message}); retrying...");
                }

                Thread.Sleep(1000 * attempt);
            }

            if (asset == null)
                throw new Exception($"Asset '{fromName}' not found in release '{version}'.");

            var req = new WebRequest(asset.Url)
            {
                Method = HttpMethod.Patch,
            };

            req.AddRaw(
                $$"""
                  { "name": "{{toName}}" }
                  """);

            req.AuthenticatedBlockingPerform();
        }
    }
}
