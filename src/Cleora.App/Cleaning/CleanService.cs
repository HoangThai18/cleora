using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cleora.App.Logging;
using Cleora.Core.Cleanup;
using Cleora.Core.Scanning;

namespace Cleora.App.Cleaning
{
    /// <summary>What a row shows after a scan.</summary>
    internal sealed record TargetResult(CleanupTarget Target, long Bytes, long Items, int NeedsAdminRoots, bool AppRunning, TargetScan? Scan)
    {
        public bool Blocked => Target.RequiresAdmin && NeedsAdminRoots > 0;
    }

    /// <summary>Scans and cleans the catalog: plain folders through Core, the Recycle Bin and Windows.old through Windows.</summary>
    internal sealed class CleanService
    {
        private readonly KnownFolders _folders;
        private readonly bool _elevated;

        public CleanService(KnownFolders folders, bool elevated)
        {
            _folders = folders;
            _elevated = elevated;
            Targets = TargetCatalog.Build(folders);
        }

        public IReadOnlyList<CleanupTarget> Targets { get; }

        public TargetResult Scan(CleanupTarget target, CancellationToken cancel)
        {
            var running = target.ProcessNames.Count > 0 && RunningApps.AnyRunning(target.ProcessNames);
            switch (target.Kind)
            {
                case TargetKind.RecycleBin:
                    var bin = RecycleBin.Query();
                    return new TargetResult(target, bin.Bytes, bin.Items, 0, false, null);
                case TargetKind.WindowsOld:
                    if (!Directory.Exists(WindowsOld.PathOn(_folders)))
                    {
                        return new TargetResult(target, 0, 0, 0, false, null);
                    }

                    // Sized by walking it (unreadable parts are left out of the total); never cleaned file by file.
                    var walker = new FileWalker();
                    long bytes = 0, count = 0;
                    foreach (var file in walker.Walk(WindowsOld.PathOn(_folders), Array.Empty<string>(), true, cancel))
                    {
                        bytes += file.Length;
                        count++;
                    }

                    return new TargetResult(target, bytes, count, _elevated ? 0 : 1, false, null);
                default:
                    var scan = CleanupScanner.Scan(target, _folders, DateTime.UtcNow, _elevated, cancel);
                    return new TargetResult(target, scan.Bytes, scan.Count, scan.NeedsAdminRoots, running, scan);
            }
        }

        public CleanResult Clean(TargetResult result, CancellationToken cancel)
        {
            try
            {
                switch (result.Target.Kind)
                {
                    case TargetKind.RecycleBin:
                        var before = RecycleBin.Query();
                        return RecycleBin.Empty() ? new CleanResult(before.Bytes, (int)Math.Min(int.MaxValue, before.Items), 0) : CleanResult.None;
                    case TargetKind.WindowsOld:
                        return _elevated && WindowsOld.Remove(_folders) ? new CleanResult(result.Bytes, (int)Math.Min(int.MaxValue, result.Items), 0) : CleanResult.None;
                    default:
                        return result.Scan == null ? CleanResult.None : new Cleaner().Clean(result.Scan, _folders, cancel);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Error("cleaning " + result.Target.Id + " failed", error);
                return CleanResult.None;
            }
        }
    }
}
