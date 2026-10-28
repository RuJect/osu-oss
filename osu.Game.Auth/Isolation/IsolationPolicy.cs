// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// The syscall policy applied to proprietary-origin callers. Ported from the former
    /// <c>authisolate.h</c>; kept as syscall names so libseccomp resolves the arch-specific
    /// numbers.
    /// </summary>
    public static class IsolationPolicy
    {
        // Denied for proprietary-origin callers (non-network).
        private static readonly string[] deny =
        {
            // process creation / launch
            "execve", "execveat", "fork", "vfork",
            // tracing / cross-process memory
            "ptrace", "process_vm_readv", "process_vm_writev",
            // namespaces / mounts
            "mount", "umount2", "pivot_root", "setns", "unshare", "chroot",
            // credentials
            "setuid", "setgid", "setreuid", "setregid", "setresuid", "setresgid", "setfsuid", "setfsgid", "setgroups",
            // kernel modules / bpf / kexec
            "init_module", "finit_module", "delete_module", "kexec_load", "kexec_file_load", "bpf", "perf_event_open", "userfaultfd",
            // kernel keyring / file handles
            "keyctl", "add_key", "request_key", "open_by_handle_at", "name_to_handle_at",
            // power
            "reboot", "swapon", "swapoff",
            // sandbox configuration
            "seccomp",
        };

        // Network syscalls denied for proprietary-origin callers (configurable).
        //
        // The send/recv family is intentionally absent: the control channel hands the listener fd
        // to the supervisor using sendmsg, so notifying it would deadlock the handoff. Network is
        // enforced at socket-creation / connect.
        private static readonly string[] network =
        {
            "socket", "socketpair", "connect", "bind", "listen", "accept", "accept4",
        };

        private static int[]? notifySyscalls;
        private static int[]? networkSyscalls;
        private static int cloneSyscall = Seccomp.SYSCALL_RESOLVE_ERROR;
        private static int clone3Syscall = Seccomp.SYSCALL_RESOLVE_ERROR;

        /// <summary>All syscalls the filter notifies on (deny set + network set).</summary>
        public static int[] NotifySyscalls => notifySyscalls ??= resolve(deny, network);

        /// <summary>The network subset of <see cref="NotifySyscalls"/>.</summary>
        public static int[] NetworkSyscalls => networkSyscalls ??= resolve(network);

        /// <summary><c>__NR_clone</c> for this architecture, or -1.</summary>
        public static int CloneSyscall => cloneSyscall;

        /// <summary><c>__NR_clone3</c> for this architecture, or -1.</summary>
        public static int Clone3Syscall => clone3Syscall;

        /// <summary>Resolves all policy names once the native library is loaded.</summary>
        public static void Resolve()
        {
            notifySyscalls ??= resolve(deny, network);
            networkSyscalls ??= resolve(network);
            cloneSyscall = Seccomp.seccomp_syscall_resolve_name("clone");
            clone3Syscall = Seccomp.seccomp_syscall_resolve_name("clone3");
        }

        private static int[] resolve(params string[][] groups)
        {
            var result = new List<int>();

            cloneSyscall = Seccomp.seccomp_syscall_resolve_name("clone");
            clone3Syscall = Seccomp.seccomp_syscall_resolve_name("clone3");

            foreach (string[] group in groups)
            {
                foreach (string name in group)
                {
                    int nr = Seccomp.seccomp_syscall_resolve_name(name);

                    if (nr != Seccomp.SYSCALL_RESOLVE_ERROR && !result.Contains(nr))
                        result.Add(nr);
                }
            }

            return result.ToArray();
        }

        /// <summary>True if <paramref name="nr"/> is one of the network policy syscalls.</summary>
        public static bool IsNetwork(int nr)
        {
            foreach (int n in NetworkSyscalls)
            {
                if (n == nr)
                    return true;
            }

            return false;
        }

        /// <summary>Architecture guard: only x86_64 filters are honoured (matches the filter's arch check).</summary>
        public const uint AUDIT_ARCH_X86_64 = 0xC000003Eu;

        /// <summary><c>CLONE_THREAD</c>; thread creation is legitimate, process creation is not.</summary>
        public const ulong CLONE_THREAD = 0x00010000UL;
    }
}
