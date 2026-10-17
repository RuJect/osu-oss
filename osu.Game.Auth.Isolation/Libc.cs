// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// The libc primitives (and CMSG helpers) needed to ferry the seccomp listener fd and the
    /// published mapping ranges between the game and the supervisor over a <c>SOCK_SEQPACKET</c>
    /// socketpair. Linux-only.
    /// </summary>
    public static unsafe class Libc
    {
        public const int AfUnix = 1;
        public const int SockSeqpacket = 5;
        public const int SockCloexec = 0x80000;

        public const int FGetFd = 1;
        public const int FSetFd = 2;
        public const int FdCloexec = 1;

        public const int SolSocket = 1;
        public const int ScmRights = 1;

        public const int MsgNosignal = 0x4000;

        public const short PollIn = 0x001;
        public const short PollHup = 0x010;

        private const int cmsghdr_size = 16; // size_t(8) + int(4) + int(4) on x86_64

        [StructLayout(LayoutKind.Sequential)]
        public struct Iovec
        {
            public void* Base;
            public nuint Length;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MsgHdr
        {
            public void* Name;
            public uint NameLen;
            public void* Iov;
            public nuint Iovlen;
            public void* Control;
            public nuint Controllen;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CMsgHdr
        {
            public nuint Len;
            public int Level;
            public int Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PollFd
        {
            public int Fd;
            public short Events;
            public short Revents;
        }

        [DllImport("libc", SetLastError = true)]
        public static extern int socketpair(int domain, int type, int protocol, int[] sv);

        [DllImport("libc", SetLastError = true)]
        public static extern int fcntl(int fd, int cmd, int arg);

        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        public static extern nint send(int fd, void* buf, nuint length, int flags);

        [DllImport("libc", SetLastError = true)]
        public static extern nint recv(int fd, void* buf, nuint length, int flags);

        [DllImport("libc", SetLastError = true)]
        public static extern nint sendmsg(int fd, MsgHdr* msg, int flags);

        [DllImport("libc", SetLastError = true)]
        public static extern nint recvmsg(int fd, MsgHdr* msg, int flags);

        [DllImport("libc", SetLastError = true)]
        public static extern int poll(PollFd* fds, nuint nfds, int timeout);

        /// <summary>Sends <paramref name="fdToSend"/> as an <c>SCM_RIGHTS</c> ancillary message.</summary>
        public static bool SendFileDescriptor(int socket, int fdToSend)
        {
            byte payload = 0x46; // 'F'
            var iov = new Iovec { Base = &payload, Length = 1 };

            byte* cbuf = stackalloc byte[cmsghdr_size + 8];
            for (int i = 0; i < cmsghdr_size + 8; i++)
                cbuf[i] = 0;

            var cmsg = (CMsgHdr*)cbuf;
            cmsg->Len = (nuint)(cmsghdr_size + sizeof(int)); // CMSG_LEN(sizeof(int))
            cmsg->Level = SolSocket;
            cmsg->Type = ScmRights;
            *(int*)(cbuf + cmsghdr_size) = fdToSend;

            var msg = new MsgHdr
            {
                Iov = &iov,
                Iovlen = 1,
                Control = cbuf,
                Controllen = (nuint)(cmsghdr_size + 8), // CMSG_SPACE(sizeof(int))
            };

            return sendmsg(socket, &msg, MsgNosignal) >= 0;
        }

        /// <summary>Receives a single fd sent with <see cref="SendFileDescriptor"/>; returns -1 on failure.</summary>
        public static int ReceiveFileDescriptor(int socket)
        {
            byte payload;
            var iov = new Iovec { Base = &payload, Length = 1 };

            byte* cbuf = stackalloc byte[cmsghdr_size + 8];
            for (int i = 0; i < cmsghdr_size + 8; i++)
                cbuf[i] = 0;

            var msg = new MsgHdr
            {
                Iov = &iov,
                Iovlen = 1,
                Control = cbuf,
                Controllen = (nuint)(cmsghdr_size + 8),
            };

            if (recvmsg(socket, &msg, 0) < 0)
                return -1;

            if (msg.Controllen < (nuint)cmsghdr_size)
                return -1;

            var cmsg = (CMsgHdr*)cbuf;

            if (cmsg->Level != SolSocket || cmsg->Type != ScmRights || cmsg->Len < (nuint)(cmsghdr_size + sizeof(int)))
                return -1;

            return *(int*)(cbuf + cmsghdr_size);
        }

        /// <summary>Sends a raw fixed-size datagram, preserving message boundaries (<c>SOCK_SEQPACKET</c>).</summary>
        public static bool SendMessage(int socket, void* buffer, nuint length)
            => send(socket, buffer, length, MsgNosignal) == (nint)length;
    }
}
