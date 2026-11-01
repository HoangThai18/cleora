using System;

namespace Cleora.Core.Startup
{
    public enum StartupLocation
    {
        /// <summary>HKCU\...\Run.</summary>
        UserRun,

        /// <summary>HKLM\...\Run (64-bit view).</summary>
        MachineRun,

        /// <summary>HKLM\WOW6432Node\...\Run.</summary>
        MachineRun32,

        /// <summary>The user's Startup folder.</summary>
        UserFolder,

        /// <summary>The Startup folder of all users.</summary>
        CommonFolder,
    }

    /// <summary>An app Windows starts at sign-in; <see cref="Name"/> is the Run value name or the shortcut file name.</summary>
    public sealed record StartupItem(string Name, string Command, StartupLocation Location, bool Enabled)
    {
        public bool RequiresAdmin => Location == StartupLocation.MachineRun || Location == StartupLocation.MachineRun32 || Location == StartupLocation.CommonFolder;
    }

    /// <summary>
    /// The on/off switch Task Manager uses (Explorer\StartupApproved): a 12-byte value whose first byte is even when
    /// enabled (2, 6) and odd when disabled (3, 7), followed by the time it was disabled. Using it instead of
    /// deleting Run entries keeps every change reversible and in step with Task Manager.
    /// </summary>
    public static class StartupApproved
    {
        public const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        public static string SubKey(StartupLocation location)
        {
            switch (location)
            {
                case StartupLocation.MachineRun32:
                    return Key + "Run32";
                case StartupLocation.UserFolder:
                case StartupLocation.CommonFolder:
                    return Key + "StartupFolder";
                default:
                    return Key + "Run";
            }
        }

        public static bool IsEnabled(byte[]? value) => value == null || value.Length == 0 || (value[0] & 1) == 0;

        public static byte[] Value(bool enabled, DateTime nowUtc)
        {
            var value = new byte[12];
            value[0] = enabled ? (byte)2 : (byte)3;
            if (!enabled)
            {
                BitConverter.GetBytes(nowUtc.ToFileTimeUtc()).CopyTo(value, 4);
            }

            return value;
        }
    }
}
