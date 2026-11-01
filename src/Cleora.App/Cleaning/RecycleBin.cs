using System;
using System.IO;
using System.Runtime.InteropServices;
using Cleora.App.Logging;

namespace Cleora.App.Cleaning
{
    /// <summary>The Recycle Bin of all drives, through the shell (its folders are per user SID and hidden).</summary>
    internal static class RecycleBin
    {
        private const int NoConfirmation = 0x1;
        private const int NoProgressUi = 0x2;
        private const int NoSound = 0x4;

        public static (long Bytes, long Items) Query()
        {
            var info = new ShQueryRbInfo { Size = Marshal.SizeOf(typeof(ShQueryRbInfo)) };
            var result = SHQueryRecycleBin(null, ref info);
            if (result != 0)
            {
                Log.Warn($"SHQueryRecycleBin failed: 0x{result:x8}");
                return (0, 0);
            }

            return (info.Bytes, info.Items);
        }

        public static bool Empty()
        {
            var result = SHEmptyRecycleBin(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);

            // E_UNEXPECTED (0x8000FFFF) is what Windows returns for an already empty bin.
            if (result != 0 && result != unchecked((int)0x8000FFFF))
            {
                Log.Warn($"SHEmptyRecycleBin failed: 0x{result:x8}");
                return false;
            }

            return true;
        }

        /// <summary>Moves a file to the Recycle Bin, so the user can still get it back.</summary>
        public static void Send(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
        }

        // 64-bit Windows packs this struct on 8 bytes (24 bytes in all); a smaller cbSize makes the call fail.
        [StructLayout(LayoutKind.Sequential)]
        private struct ShQueryRbInfo
        {
            public int Size;
            public long Bytes;
            public long Items;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? rootPath, ref ShQueryRbInfo info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr window, string? rootPath, int flags);
    }
}
