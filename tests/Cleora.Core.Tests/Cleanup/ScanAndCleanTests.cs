using Cleora.Core.Cleanup;
using Cleora.Core.Scanning;

namespace Cleora.Core.Tests.Cleanup
{
    public class ScanAndCleanTests
    {
        private static CleanupTarget Target(TimeSpan minAge, params CleanupRoot[] roots) =>
            new CleanupTarget("t", TargetGroup.System, "t", "t", roots, minAge, true);

        [Fact]
        public void Scan_SkipsFilesYoungerThanMinAge()
        {
            using var s = new Scratch();
            var temp = s.Dir("C/Users/ban/AppData/Local/Temp");
            s.File("C/Users/ban/AppData/Local/Temp/old.tmp", 100, DateTime.UtcNow.AddDays(-3));
            s.File("C/Users/ban/AppData/Local/Temp/installer-running.tmp", 100, DateTime.UtcNow.AddMinutes(-5));

            var scan = CleanupScanner.Scan(Target(TimeSpan.FromHours(24), CleanupRoot.All(temp)), s.Folders, DateTime.UtcNow, elevated: false);

            Assert.Equal(new[] { "old.tmp" }, scan.Files.Select(f => Path.GetFileName(f.Path)));
            Assert.Equal(100, scan.Bytes);
        }

        [Fact]
        public void Scan_AdminRootsWithoutRights_AreCountedNotRead()
        {
            using var s = new Scratch();
            var windowsTemp = s.Dir("C/Windows/Temp");
            s.File("C/Windows/Temp/a.log");

            var scan = CleanupScanner.Scan(Target(TimeSpan.Zero, CleanupRoot.All(windowsTemp, admin: true)), s.Folders, DateTime.UtcNow, elevated: false);

            Assert.Empty(scan.Files);
            Assert.Equal(1, scan.NeedsAdminRoots);
        }

        [Fact]
        public void Scan_NonRecursiveRoot_OnlyMatchingFilesDirectlyInside()
        {
            using var s = new Scratch();
            var explorer = s.Dir("C/Users/ban/AppData/Local/Microsoft/Windows/Explorer");
            s.File("C/Users/ban/AppData/Local/Microsoft/Windows/Explorer/thumbcache_256.db");
            s.File("C/Users/ban/AppData/Local/Microsoft/Windows/Explorer/ExplorerStartupLog.etl");
            s.File("C/Users/ban/AppData/Local/Microsoft/Windows/Explorer/sub/thumbcache_32.db");

            var scan = CleanupScanner.Scan(Target(TimeSpan.Zero, CleanupRoot.Only(explorer, false, "thumbcache_*.db")), s.Folders, DateTime.UtcNow, elevated: false);

            Assert.Equal(new[] { "thumbcache_256.db" }, scan.Files.Select(f => Path.GetFileName(f.Path)));
        }

        [Fact]
        public void Scan_DoesNotFollowALinkedFolder()
        {
            using var s = new Scratch();
            var cache = s.Dir("C/Users/ban/AppData/Local/Temp");
            var documents = s.Dir("C/Users/ban/Documents");
            s.File("C/Users/ban/Documents/thesis.docx", 5000);
            Directory.CreateSymbolicLink(Path.Combine(cache, "docs-link"), documents);

            var scan = CleanupScanner.Scan(Target(TimeSpan.Zero, CleanupRoot.All(cache)), s.Folders, DateTime.UtcNow, elevated: false);
            new Cleaner().Clean(scan, s.Folders);

            Assert.Empty(scan.Files);
            Assert.True(File.Exists(Path.Combine(documents, "thesis.docx")));
        }

        [Fact]
        public void Clean_DeletesFilesAndEmptyFolders_KeepsTheRoot()
        {
            using var s = new Scratch();
            var cache = s.Dir("C/Users/ban/AppData/Roaming/discord/Cache");
            s.File("C/Users/ban/AppData/Roaming/discord/Cache/Cache_Data/a", 100);
            s.File("C/Users/ban/AppData/Roaming/discord/Cache/Cache_Data/b", 50);
            var target = Target(TimeSpan.Zero, CleanupRoot.All(cache));

            var result = new Cleaner().Clean(CleanupScanner.Scan(target, s.Folders, DateTime.UtcNow, false), s.Folders);

            Assert.Equal(new CleanResult(150, 2, 0), result);
            Assert.True(Directory.Exists(cache));
            Assert.False(Directory.Exists(Path.Combine(cache, "Cache_Data")));
        }

        [Fact]
        public void Clean_FileInUse_IsCountedAndTheRestStillGoes()
        {
            using var s = new Scratch();
            var cache = s.Dir("C/Users/ban/AppData/Local/Temp");
            var locked = s.File("C/Users/ban/AppData/Local/Temp/locked.tmp", 70);
            s.File("C/Users/ban/AppData/Local/Temp/free.tmp", 30);
            var target = Target(TimeSpan.Zero, CleanupRoot.All(cache));
            var cleaner = new Cleaner(path =>
            {
                if (path == locked)
                {
                    throw new IOException("in use");
                }

                File.Delete(path);
            });

            var result = cleaner.Clean(CleanupScanner.Scan(target, s.Folders, DateTime.UtcNow, false), s.Folders);

            Assert.Equal(new CleanResult(30, 1, 1), result);
            Assert.True(File.Exists(locked));
        }

        [Fact]
        public void Clean_AFileOutsideItsRoot_IsNeverDeleted()
        {
            using var s = new Scratch();
            var cache = s.Dir("C/Users/ban/AppData/Local/Temp");
            var precious = s.File("C/Users/ban/Documents/thesis.docx", 5000);
            var forged = new TargetScan(Target(TimeSpan.Zero, CleanupRoot.All(cache)), new[] { new FoundFile(precious, 5000, DateTime.UtcNow) }, 5000, 0, 0);

            var result = new Cleaner().Clean(forged, s.Folders);

            Assert.Equal(CleanResult.None, result);
            Assert.True(File.Exists(precious));
        }

        [Fact]
        public void Clean_NonRecursiveRoot_RefusesFilesInSubfolders()
        {
            using var s = new Scratch();
            var windows = s.Dir("C/Windows");
            var deep = s.File("C/Windows/System32/config/SYSTEM", 100);
            var dump = s.File("C/Windows/MEMORY.DMP", 900);
            var target = Target(TimeSpan.Zero, CleanupRoot.Only(windows, true, "MEMORY.DMP"));
            var forged = new TargetScan(target, new[] { new FoundFile(deep, 100, DateTime.UtcNow), new FoundFile(dump, 900, DateTime.UtcNow) }, 1000, 0, 0);

            var result = new Cleaner().Clean(forged, s.Folders);

            Assert.Equal(new CleanResult(900, 1, 0), result);
            Assert.True(File.Exists(deep));
            Assert.True(Directory.Exists(windows));
        }

        [Fact]
        public void Clean_UnsafeRoot_DeletesNothing()
        {
            using var s = new Scratch();
            var file = s.File("C/Users/ban/notes.txt");
            var target = Target(TimeSpan.Zero, CleanupRoot.All(s.Folders.UserProfile));
            var forged = new TargetScan(target, new[] { new FoundFile(file, 10, DateTime.UtcNow) }, 10, 0, 0);

            Assert.Empty(CleanupScanner.Scan(target, s.Folders, DateTime.UtcNow, true).Files);
            Assert.Equal(CleanResult.None, new Cleaner().Clean(forged, s.Folders));
            Assert.True(File.Exists(file));
        }
    }
}
