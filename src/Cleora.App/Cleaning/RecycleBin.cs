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

        /// <summary>
        /// Moves a file to the Recycle Bin. Only files on fixed drives (USB sticks and network shares have no bin),
        /// and Windows is told to warn before destroying a file it can't recycle (bigger than the bin, bin turned off)
        /// instead of deleting it silently. True when the file is in the bin.
        /// </summary>
        public static bool Send(string path)
        {
            if (!File.Exists(path) || !OnFixedDrive(path))
            {
                return false;
            }

            var operation = new ShFileOpStruct
            {
                Func = FoDelete,
                From = System.IO.Path.GetFullPath(path) + "\0\0",
                Flags = FofAllowUndo | FofNoConfirmation | FofWantNukeWarning | FofSilent,
            };
            var result = SHFileOperation(ref operation);
            var recycled = result == 0 && !operation.AnyOperationsAborted && !File.Exists(path);
            if (!recycled)
            {
                Log.Warn($"recycle {path}: result 0x{result:x}, aborted {operation.AnyOperationsAborted}");
            }

            return recycled;
        }

        public static bool OnFixedDrive(string path)
        {
            try
            {
                var root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path));
                return !string.IsNullOrEmpty(root) && !root!.StartsWith("\\\\", StringComparison.Ordinal) && new DriveInfo(root).DriveType == DriveType.Fixed;
            }
            catch (Exception error) when (error is ArgumentException || error is IOException)
            {
                return false;
            }
        }

        private const int FoDelete = 3;
        private const ushort FofSilent = 0x4;
        private const ushort FofNoConfirmation = 0x10;
        private const ushort FofAllowUndo = 0x40;
        private const ushort FofWantNukeWarning = 0x4000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileOpStruct
        {
            public IntPtr Window;
            public int Func;
            [MarshalAs(UnmanagedType.LPWStr)] public string From;
            [MarshalAs(UnmanagedType.LPWStr)] public string? To;
            public ushort Flags;
            [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
            public IntPtr NameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref ShFileOpStruct operation);

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
