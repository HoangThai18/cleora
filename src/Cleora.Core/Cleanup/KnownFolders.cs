using System;
using System.IO;

namespace Cleora.Core.Cleanup
{
    /// <summary>The Windows folders the catalog is built from. Tests point them at a scratch tree.</summary>
    public sealed record KnownFolders(
        string SystemDrive,
        string Windows,
        string ProgramData,
        string ProgramFilesX86,
        string UserProfile,
        string LocalAppData,
        string RoamingAppData,
        string Temp)
    {
        /// <summary>Every folder Cleora must never empty or delete as a whole.</summary>
        public string[] Protected => new[]
        {
            SystemDrive, Windows, ProgramData, ProgramFilesX86, UserProfile, LocalAppData, RoamingAppData,
            Path.Combine(Windows, "System32"), Path.Combine(Windows, "SysWOW64"), Path.Combine(Windows, "WinSxS"),
        };
    }
}
