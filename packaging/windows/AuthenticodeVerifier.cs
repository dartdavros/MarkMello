using System;
using System.Runtime.InteropServices;

namespace MarkMello.Packaging
{
    public static class AuthenticodeVerifier
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FileInfo
        {
            public uint Size;
            public IntPtr Path;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            public uint Size;
            public IntPtr PolicyCallback;
            public IntPtr SipClient;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr File;
            public uint StateAction;
            public IntPtr State;
            public IntPtr Url;
            public uint ProviderFlags;
            public uint UiContext;
            public IntPtr SignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true)]
        private static extern uint WinVerifyTrust(
            IntPtr window, ref Guid action, ref TrustData data);

        public static uint Verify(string path)
        {
            var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            var file = new FileInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(FileInfo)),
                Path = Marshal.StringToCoTaskMemUni(path)
            };
            var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf(typeof(TrustData)),
                UiChoice = 2, // WTD_UI_NONE: never display certificate dialogs.
                UnionChoice = 1, // WTD_CHOICE_FILE: verify the embedded signature.
                File = filePointer,
                StateAction = 1 // WTD_STATEACTION_VERIFY.
            };

            try
            {
                Marshal.StructureToPtr(file, filePointer, false);
                return WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            }
            finally
            {
                data.StateAction = 2; // WTD_STATEACTION_CLOSE.
                WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                Marshal.FreeHGlobal(filePointer);
                Marshal.FreeCoTaskMem(file.Path);
            }
        }
    }
}
