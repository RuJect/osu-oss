// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// The slice of <c>libseccomp</c> used by the auth isolation.
    /// </summary>
    /// <remarks>
    /// This is the only filter-construction dependency: the seccomp BPF program and the
    /// user-notification superstructure are both built by libseccomp, so the policy can be
    /// expressed in terms of syscall names without hand-rolled, arch-specific BPF.
    /// </remarks>
    public static class Seccomp
    {
        private const string library = "libseccomp.so.2";

        /// <summary><c>SCMP_ACT_ALLOW</c>.</summary>
        public const uint ActAllow = 0x7fff0000u;

        /// <summary><c>SCMP_ACT_NOTIFY</c>.</summary>
        public const uint ActNotify = 0x7fc00000u;

        /// <summary><c>SCMP_FLTATR_CTL_NNP</c> (load the filter with <c>NO_NEW_PRIVS</c>).</summary>
        public const int FltAttrCtlNnp = 3;

        /// <summary><c>SCMP_FLTATR_CTL_TSYNC</c> (synchronise all threads on load).</summary>
        public const int FltAttrCtlTsync = 4;

        /// <summary>
        /// libseccomp API level that first supports <see cref="FltAttrCtlTsync"/> together with
        /// the notify APIs. See <c>seccomp_api_get(3)</c>.
        /// </summary>
        public const uint ApiTsyncAndNotify = 6;

        /// <summary><c>SECCOMP_USER_NOTIF_FLAG_CONTINUE</c>.</summary>
        public const uint UserNotifFlagContinue = 1;

        /// <summary>Sentinel returned by <see cref="seccomp_syscall_resolve_name"/> on failure.</summary>
        public const int SyscallResolveError = -1;

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint seccomp_api_get();

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr seccomp_init(uint defAction);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int seccomp_release(IntPtr ctx);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int seccomp_attr_set(IntPtr ctx, int attr, uint value);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int seccomp_syscall_resolve_name([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int seccomp_rule_add(IntPtr ctx, uint action, int syscall, uint argCnt);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int seccomp_load(IntPtr ctx);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern int seccomp_notify_fd(IntPtr ctx);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int seccomp_notify_alloc(out IntPtr req, out IntPtr resp);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
        public static extern void seccomp_notify_free(IntPtr req, IntPtr resp);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int seccomp_notify_receive(int fd, IntPtr req);

        [DllImport(library, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int seccomp_notify_respond(int fd, IntPtr resp);
    }

    /// <summary>Mirror of <c>struct seccomp_data</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SeccompData
    {
        public int Nr;
        public uint Arch;
        public ulong InstructionPointer;
        public ulong Arg0;
        public ulong Arg1;
        public ulong Arg2;
        public ulong Arg3;
        public ulong Arg4;
        public ulong Arg5;
    }

    /// <summary>Mirror of <c>struct seccomp_notif</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SeccompNotif
    {
        public ulong Id;
        public uint Pid;
        public uint Flags;
        public SeccompData Data;
    }

    /// <summary>Mirror of <c>struct seccomp_notif_resp</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SeccompNotifResp
    {
        public ulong Id;
        public long Val;
        public int Error;
        public uint Flags;
    }
}
