// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;

namespace osu.Game.Auth
{
    /// <summary>
    /// Manages the native auth module (<c>AuthNative</c>): storage resolution,
    /// loading, entry-point resolution, signing and updates.
    /// Clean-room reimplementation of the proprietary manager: same observable
    /// behaviour (file layout, update protocol, call discipline, log messages),
    /// no VM.
    /// </summary>
    /// <remarks>
    /// The proprietary manager resolves its exports by name and invokes them
    /// through <see cref="Marshal.GetDelegateForFunctionPointer{TDelegate}(IntPtr)"/>:
    /// <list type="bullet">
    /// <item><description><c>Initialize()</c> -> <see cref="int"/> result; a non-zero result is reported and aborts initialisation.</description></item>
    /// <item><description><c>Sign(int, IntPtr)</c> -> signs a managed request/options object in place.</description></item>
    /// <item><description><c>Unload()</c> -> releases native resources.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class NativeModule : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NativeInitializeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeSignDelegate(int opcode, IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeReportDelegate(IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeUnloadDelegate();

        private readonly object syncRoot = new object();

        private readonly Storage storage;
        private readonly Func<string?> accessToken;
        private readonly Func<bool> isTachyon;

        private readonly StartupState startupState;
        private readonly NativeModuleUpdater updater;

        private IntPtr nativeHandle;
        private NativeUnloadDelegate? unloadDelegate;
        private NativeSignDelegate? signDelegate;
        private NativeReportDelegate? reportDelegate;

        public event Action? Initialized;
        public event Action? Reconnected;

        public string FileName { get; }

        public string? CurrentHash { get; private set; }

        public bool IsInitialised => signDelegate != null;

        public NativeModule(GameHost host, OsuGame game, Func<string?> accessToken, Func<bool> isTachyon)
        {
            this.accessToken = accessToken;
            this.isTachyon = isTachyon;

            FileName = "AuthNative" + GetNativeExtension();
            storage = ResolveStorage(host);

            startupState = new StartupState(storage);
            ValidateState();

            updater = new NativeModuleUpdater(storage, FileName, accessToken, isTachyon,
                () => CurrentHash,
                hash => CurrentHash = hash,
                RequestRestartAsync,
                PerformUpdateAsync,
                OnNativeUpdatedAsync);
            updater.RunBackgroundLoop();

            InitialiseNative();
        }

        private Task OnNativeUpdatedAsync()
        {
            // The updater fetched the module mid-session; (re)try initialisation now.
            if (!IsInitialised)
                InitialiseNative();

            if (IsInitialised)
                Reconnected?.Invoke();

            return Task.CompletedTask;
        }

        private static string GetNativeExtension()
            => OperatingSystem.IsWindows() ? ".dll" : ".so";

        internal static int GetPlatformId()
        {
            if (OperatingSystem.IsWindows())
                return 1;

            if (OperatingSystem.IsLinux())
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => 2,
                    Architecture.Arm64 => 3,
                    _ => 0,
                };
            }

            if (OperatingSystem.IsMacOS())
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => 4,
                    Architecture.Arm64 => 5,
                    _ => 0,
                };
            }

            return 0;
        }

        internal static Storage ResolveStorage(GameHost host)
        {
            foreach (string path in host.UserStoragePaths)
            {
                Storage storage = host.GetStorage(path);

                if (storage.ExistsDirectory("osu"))
                    return storage.GetStorageForDirectory("osu");
            }

            foreach (string path in host.UserStoragePaths)
            {
                try
                {
                    return host.GetStorage(path).GetStorageForDirectory("osu");
                }
                catch
                {
                    // try next path.
                }
            }

            throw new InvalidOperationException("No valid user storage path could be resolved.");
        }

        private void ValidateState()
        {
            try
            {
                if ((startupState.HasPreviousStartup() && !startupState.PreviousStartupContains(StartupState.LOAD_SUCCESS))
                    || storage.Exists(NativeModuleUpdater.REQUIRE_UPDATE_FILE_NAME))
                {
                    try { storage.Delete(FileName); }
                    catch (Exception e) { Logger.Log($"Failed to validate state: {e}", LoggingTarget.Network); }

                    try { storage.Delete(NativeModuleUpdater.REQUIRE_UPDATE_FILE_NAME); }
                    catch (Exception e) { Logger.Log($"Failed to validate state: {e}", LoggingTarget.Network); }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to validate state: {e}", LoggingTarget.Network);
            }
        }

        /// <summary>
        /// Loads the native module and resolves/invokes its exports, mirroring the
        /// proprietary manager's load + initialise flow.
        /// </summary>
        private void InitialiseNative()
        {
            lock (syncRoot)
            {
                if (IsInitialised)
                {
                    Logger.Log("Module already initialized.", LoggingTarget.Network);
                    return;
                }

                Logger.Log("Loading module...", LoggingTarget.Network);

                if (!storage.Exists(FileName))
                {
                    Logger.Log("Module not present.", LoggingTarget.Network);
                    return;
                }

                string fullPath = storage.GetFullPath(FileName);

                try
                {
                    if (nativeHandle == IntPtr.Zero && !NativeLibrary.TryLoad(fullPath, out nativeHandle))
                    {
                        nativeHandle = IntPtr.Zero;
                        Logger.Log("Module could not be loaded.", LoggingTarget.Network);
                        updater.ForceUpdate();
                        return;
                    }

                    nint initAddr = GetExport("Initialize");
                    nint unloadAddr = GetExport("Unload");

                    if (initAddr == IntPtr.Zero || unloadAddr == IntPtr.Zero)
                    {
                        Logger.Log("Module initialization failed, critical functions missing.", LoggingTarget.Network);
                        return;
                    }

                    unloadDelegate = Marshal.GetDelegateForFunctionPointer<NativeUnloadDelegate>(unloadAddr);

                    int result = Marshal.GetDelegateForFunctionPointer<NativeInitializeDelegate>(initAddr)();

                    if (result != 0)
                    {
                        Logger.Log($"Module initialization failed with result: {result}", LoggingTarget.Network);
                        unloadDelegate = null;
                        return;
                    }

                    nint signAddr = GetExport("Sign");
                    if (signAddr != IntPtr.Zero)
                        signDelegate = Marshal.GetDelegateForFunctionPointer<NativeSignDelegate>(signAddr);

                    nint reportAddr = GetExport("Report");
                    if (reportAddr != IntPtr.Zero)
                        reportDelegate = Marshal.GetDelegateForFunctionPointer<NativeReportDelegate>(reportAddr);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to load module: {e}", LoggingTarget.Network);
                    unloadDelegate = null;
                    signDelegate = null;
                    reportDelegate = null;
                    return;
                }

                Logger.Log("Module initialized successfully.", LoggingTarget.Network);
                startupState.Add(StartupState.LOAD_SUCCESS);

                if (IsInitialised)
                    Initialized?.Invoke();
            }
        }

        /// <summary>Resolves an export by name, matching the proprietary manager.</summary>
        private nint GetExport(string name)
        {
            if (nativeHandle != IntPtr.Zero && NativeLibrary.TryGetExport(nativeHandle, name, out nint address))
                return address;

            return IntPtr.Zero;
        }

        /// <summary>Signs a request object in place (headers are mutated by native code).</summary>
        /// <param name="target">The kind of object being signed.</param>
        /// <param name="request">The request object (web request or connection options).</param>
        public void Sign(SignTarget target, object request)
        {
            var sign = signDelegate;

            if (sign == null)
                return;

            lock (syncRoot)
            {
                var handle = GCHandle.Alloc(request);
                try
                {
                    sign((int)target, GCHandle.ToIntPtr(handle));
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
        }

        /// <summary>Reports a managed event object to native code.</summary>
        public void Report(object evt)
        {
            var report = reportDelegate;
            if (report == null)
                return;

            lock (syncRoot)
            {
                var handle = GCHandle.Alloc(evt);
                try
                {
                    report(GCHandle.ToIntPtr(handle));
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
        }

        private Task RequestRestartAsync() => NotificationRelay.PostAsync(
            "Please restart the game to enable full online functionality.");

        private Task PerformUpdateAsync(string tempFile)
        {
            File.Move(tempFile, storage.GetFullPath(FileName), overwrite: true);
            return Task.CompletedTask;
        }

        public Task CheckForUpdateAsync() => updater.CheckForUpdateAsync();

        /// <summary>
        /// Releases the native module, mirroring the proprietary unload flow.
        /// </summary>
        private void UnloadNative()
        {
            lock (syncRoot)
            {
                if (unloadDelegate == null && signDelegate == null)
                {
                    Logger.Log("Attempted unload when not loaded, ignoring.", LoggingTarget.Network);
                    return;
                }

                Logger.Log("Unloading module...", LoggingTarget.Network);

                try
                {
                    unloadDelegate?.Invoke();
                }
                catch (Exception e)
                {
                    updater.ForceUpdate();
                    Logger.Log($"Failed to unload module: {e}", LoggingTarget.Network);
                    return;
                }
                finally
                {
                    unloadDelegate = null;
                    signDelegate = null;
                    reportDelegate = null;
                }

                if (storage.Exists(FileName))
                    Logger.Log("Module unloaded successfully.", LoggingTarget.Network);
                else
                    Logger.Log("Could not unload module.", LoggingTarget.Network);
            }
        }

        public void Dispose()
        {
            UnloadNative();
            updater.Dispose();
        }
    }
}
