// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Auth
{
    /// <summary>
    /// Keeps the native auth module up to date via <c>auth.ppy.sh</c>.
    /// Clean-room port of the proprietary updater (same protocol).
    /// </summary>
    internal sealed class NativeModuleUpdater : IDisposable
    {
        public const string REQUIRE_UPDATE_FILE_NAME = ".require_update";

        private const string update_endpoint = "https://auth.ppy.sh/updates/get";
        private const string json_content_type = "application/json";

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private readonly Storage storage;
        private readonly string fileName;
        private readonly Func<string?> accessToken;
        private readonly Func<bool> isTachyon;
        private readonly Func<string?> getCurrentHash;
        private readonly Action<string?> setCurrentHash;
        private readonly Func<Task> requestRestart;
        private readonly Func<string, Task> performUpdate;
        private readonly Func<Task> onUpdated;

        private readonly CancellationTokenSource lifetimeCts = new CancellationTokenSource();
        private readonly CancellationToken lifetimeToken;

        private CancellationTokenSource checkCts = new CancellationTokenSource();

        public NativeModuleUpdater(Storage storage, string fileName, Func<string?> accessToken, Func<bool> isTachyon,
            Func<string?> getCurrentHash, Action<string?> setCurrentHash,
            Func<Task> requestRestart, Func<string, Task> performUpdate, Func<Task> onUpdated)
        {
            this.storage = storage;
            this.fileName = fileName;
            this.accessToken = accessToken;
            this.isTachyon = isTachyon;
            this.getCurrentHash = getCurrentHash;
            this.setCurrentHash = setCurrentHash;
            this.requestRestart = requestRestart;
            this.performUpdate = performUpdate;
            this.onUpdated = onUpdated;

            lifetimeToken = lifetimeCts.Token;

            try
            {
                if (!storage.Exists(fileName))
                    return;

                using var sha = SHA1.Create();
                using Stream stream = storage.GetStream(fileName, FileAccess.Read, FileMode.Open);
                setCurrentHash(Convert.ToHexString(sha.ComputeHash(stream)));
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to generate initial checksum: {e}", LoggingTarget.Network);
            }
        }

        public void RunBackgroundLoop()
        {
            var thread = new Thread(loop) { IsBackground = true };
            thread.Start();
        }

        private void loop()
        {
            while (!lifetimeToken.IsCancellationRequested)
            {
                try
                {
                    CheckForUpdateAsync().Wait(lifetimeToken);
                    lifetimeToken.WaitHandle.WaitOne(60000);
                }
                catch
                {
                    // ignored.
                }
            }
        }

        public async Task CheckForUpdateAsync()
        {
            using var timeoutCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken, timeoutCts.Token);

            try
            {
                await Interlocked.Exchange(ref checkCts, timeoutCts).CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                await semaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);

                try
                {
                    Logger.Log($"Checking for any update ({getCurrentHash()})...", LoggingTarget.Network);

                    NativeModuleUpdateResponse response = await getUpdateResponseAsync(linkedCts.Token).ConfigureAwait(false);

                    if (response.Restart)
                    {
                        if (!storage.Exists(REQUIRE_UPDATE_FILE_NAME))
                        {
                            Logger.Log("Update exists but requires a restart.", LoggingTarget.Network);
                            ForceUpdate();
                            await requestRestart().ConfigureAwait(false);
                        }

                        return;
                    }

                    if (response.Url == null)
                    {
                        Logger.Log("Already up to date.", LoggingTarget.Network);
                        return;
                    }

                    Logger.Log($"Update exists ({response.Checksum}).", LoggingTarget.Network);

                    var (tempFile, hash) = await downloadUpdateAsync(response, linkedCts.Token).ConfigureAwait(false);
                    await performUpdate(tempFile).ConfigureAwait(false);
                    setCurrentHash(hash);
                    await onUpdated().ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Update cancelled.", LoggingTarget.Network);
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to update: {e}", LoggingTarget.Network);
            }
        }

        public void ForceUpdate()
        {
            try
            {
                using (File.Create(storage.GetFullPath(REQUIRE_UPDATE_FILE_NAME)))
                {
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to force update: {e}", LoggingTarget.Network);
            }
        }

        private async Task<NativeModuleUpdateResponse> getUpdateResponseAsync(CancellationToken cancellationToken)
        {
            using var request = new JsonWebRequest<NativeModuleUpdateResponse>(update_endpoint)
            {
                Method = HttpMethod.Post,
                ContentType = json_content_type,
            };

            request.AddRaw(JsonConvert.SerializeObject(new NativeModuleUpdateRequest
            {
                FileName = fileName,
                CurrentHash = getCurrentHash(),
                PlatformId = NativeModule.GetPlatformId(),
                IsTachyon = isTachyon(),
            }));

            string? bearer = accessToken();
            if (!string.IsNullOrEmpty(bearer))
                request.AddHeader("Authorization", "Bearer " + bearer);

            await request.PerformAsync(cancellationToken).ConfigureAwait(false);

            if (request.Aborted)
                throw new OperationCanceledException("Request aborted.");

            return request.ResponseObject;
        }

        private async Task<(string tempFile, string hash)> downloadUpdateAsync(NativeModuleUpdateResponse response, CancellationToken token)
        {
            string tempFile = Path.GetTempFileName();
            Logger.Log($"Downloading update to {tempFile}...", LoggingTarget.Network);

            using var request = new FileWebRequest(tempFile, response.Url!);
            await request.PerformAsync(token).ConfigureAwait(false);

            if (request.Aborted)
                throw new OperationCanceledException("Request aborted.");

            using var stream = File.OpenRead(tempFile);
            using var sha = SHA1.Create();
            string hash = Convert.ToHexString(await sha.ComputeHashAsync(stream, token).ConfigureAwait(false));

            if (hash != response.Checksum)
                throw new Exception("Integrity check failed. File is likely corrupt.");

            return (tempFile, hash);
        }

        public void Dispose()
        {
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
        }
    }
}
