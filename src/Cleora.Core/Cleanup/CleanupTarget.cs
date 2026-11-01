using System;
using System.Collections.Generic;
using System.Linq;

namespace Cleora.Core.Cleanup
{
    public enum TargetGroup
    {
        System,
        Browser,
        App,
    }

    public enum TargetKind
    {
        /// <summary>Files under the roots, matched by pattern.</summary>
        Files,

        /// <summary>The Recycle Bin of every drive, emptied through the shell.</summary>
        RecycleBin,

        /// <summary>The previous Windows installation (Windows.old), removed as a whole.</summary>
        WindowsOld,
    }

    /// <summary>
    /// One place junk piles up. <see cref="Recursive"/> false means only the files directly in <see cref="Path"/>;
    /// patterns are file-name wildcards (<c>*</c>, <c>?</c>).
    /// </summary>
    public sealed record CleanupRoot(string Path, IReadOnlyList<string> Patterns, bool Recursive, bool RequiresAdmin)
    {
        public static CleanupRoot All(string path, bool admin = false) => new CleanupRoot(path, new[] { "*" }, true, admin);

        public static CleanupRoot Only(string path, bool admin, params string[] patterns) => new CleanupRoot(path, patterns, false, admin);

        public bool TakesEverything => Recursive || Patterns.Any(p => p == "*" || p == "*.*");
    }

    /// <summary>
    /// A row in the cleaning list: what it is, where it lives, how old a file must be to go, and which programs
    /// keep its files open (named in the "close it first" hint).
    /// </summary>
    public sealed record CleanupTarget(
        string Id,
        TargetGroup Group,
        string Name,
        string Detail,
        IReadOnlyList<CleanupRoot> Roots,
        TimeSpan MinAge,
        bool DefaultOn,
        TargetKind Kind = TargetKind.Files,
        IReadOnlyList<string>? Processes = null)
    {
        public bool RequiresAdmin => Roots.Count > 0 && Roots.All(r => r.RequiresAdmin);

        public IReadOnlyList<string> ProcessNames => Processes ?? Array.Empty<string>();
    }
}
