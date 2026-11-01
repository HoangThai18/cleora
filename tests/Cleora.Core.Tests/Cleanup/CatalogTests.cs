using Cleora.Core.Cleanup;

namespace Cleora.Core.Tests.Cleanup
{
    public class CatalogTests
    {
        /// <summary>Chrome with two profiles, Firefox, Discord; no Edge, no Zalo.</summary>
        internal static void FakeMachine(Scratch s)
        {
            var chrome = "C/Users/ban/AppData/Local/Google/Chrome/User Data/";
            s.File(chrome + "Default/Preferences");
            s.File(chrome + "Default/Login Data", 500);
            s.File(chrome + "Default/History", 500);
            s.File(chrome + "Default/Cache/Cache_Data/f_000001", 4000);
            s.File(chrome + "Default/Code Cache/js/abc", 1000);
            s.File(chrome + "Profile 1/Preferences");
            s.File(chrome + "Profile 1/Cache/Cache_Data/f_000002", 2000);
            s.File(chrome + "ShaderCache/data_0", 300);
            s.File(chrome + "Local State");
            s.File("C/Users/ban/AppData/Local/Mozilla/Firefox/Profiles/x1.default/cache2/entries/AB12", 700);
            s.File("C/Users/ban/AppData/Roaming/Mozilla/Firefox/Profiles/x1.default/logins.json", 50);
            s.File("C/Users/ban/AppData/Roaming/discord/Cache/Cache_Data/data_1", 900);
            s.File("C/Users/ban/AppData/Roaming/discord/Local Storage/leveldb/000003.log", 90);
            s.File("C/Users/ban/AppData/Local/Temp/setup.tmp", 100);
            s.File("C/Windows/Temp/old.log", 100);
        }

        [Fact]
        public void Build_ChromeProfiles_OnlyCacheFoldersAreRoots()
        {
            using var s = new Scratch();
            FakeMachine(s);

            var chrome = TargetCatalog.Build(s.Folders).Single(t => t.Id == "chrome");
            var roots = chrome.Roots.Select(r => Path.GetRelativePath(s.Root, r.Path).Replace('\\', '/')).ToList();

            Assert.Contains("C/Users/ban/AppData/Local/Google/Chrome/User Data/Default/Cache", roots);
            Assert.Contains("C/Users/ban/AppData/Local/Google/Chrome/User Data/Default/Code Cache", roots);
            Assert.Contains("C/Users/ban/AppData/Local/Google/Chrome/User Data/Profile 1/Cache", roots);
            Assert.Contains("C/Users/ban/AppData/Local/Google/Chrome/User Data/ShaderCache", roots);
            Assert.Equal(new[] { "chrome" }, chrome.ProcessNames);
        }

        [Fact]
        public void Scan_Browsers_NeverReachPasswordsHistoryOrCookies()
        {
            using var s = new Scratch();
            FakeMachine(s);

            var files = TargetCatalog.Build(s.Folders)
                .Where(t => t.Group != TargetGroup.System)
                .SelectMany(t => CleanupScanner.Scan(t, s.Folders, DateTime.UtcNow, elevated: true).Files)
                .Select(f => Path.GetFileName(f.Path))
                .ToList();

            Assert.Contains("f_000001", files);
            Assert.Contains("AB12", files);
            Assert.Contains("data_1", files);
            Assert.DoesNotContain("Login Data", files);
            Assert.DoesNotContain("History", files);
            Assert.DoesNotContain("logins.json", files);
            Assert.DoesNotContain("000003.log", files);
            Assert.DoesNotContain("Preferences", files);
        }

        [Fact]
        public void Build_AppsNotInstalled_AreLeftOut()
        {
            using var s = new Scratch();
            FakeMachine(s);

            var ids = TargetCatalog.Build(s.Folders).Select(t => t.Id).ToList();

            Assert.Contains("discord", ids);
            Assert.DoesNotContain("edge", ids);
            Assert.DoesNotContain("zalo", ids);
            Assert.Contains("temp", ids);
            Assert.Contains("recycle-bin", ids);
        }

        [Fact]
        public void Build_IdsAreUnique_AndRiskyOnesStartOff()
        {
            using var s = new Scratch();
            FakeMachine(s);

            var targets = TargetCatalog.Build(s.Folders);

            Assert.Equal(targets.Count, targets.Select(t => t.Id).Distinct().Count());
            Assert.False(targets.Single(t => t.Id == "windows-old").DefaultOn);
            Assert.False(targets.Single(t => t.Id == "shader-cache").DefaultOn);
            Assert.False(targets.Single(t => t.Id == "recycle-bin").DefaultOn);
            Assert.True(targets.Single(t => t.Id == "windows-old").RequiresAdmin);
        }
    }
}
