using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cleora.App.Cleaning;
using Cleora.App.Platform;
using Cleora.Core;
using Cleora.Core.Cleanup;
using Cleora.Core.Scanning;
using Cleora.Core.Startup;
using Microsoft.Win32;

namespace Cleora.App
{
    /// <summary>
    /// <c>Cleora.exe --selftest &lt;dir&gt;</c> on a real Windows (CI): cleans a fake machine built in &lt;dir&gt; end to end,
    /// scans this machine without deleting anything, reads apps and startup entries, flips a startup switch of its own
    /// and back, and round-trips a file through the Recycle Bin. Exit 0 when every check holds.
    /// </summary>
    internal static class SelfTest
    {
        private static int _failures;

        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Section("fake machine: scan and clean", () => FakeMachine(Path.Combine(directory, "fake")));
            Section("this machine: scan only", ScanThisMachine);
            Section("large files and duplicates", () => LargeAndDuplicates(Path.Combine(directory, "files")));
            Section("installed apps", InstalledAppsCheck);
            Section("startup switch", StartupCheck);
            Section("recycle bin", () => RecycleCheck(Path.Combine(directory, "recycle")));
            Console.WriteLine(_failures == 0 ? "selftest OK" : $"selftest FAILED: {_failures} check(s)");
            return _failures == 0 ? 0 : 1;
        }

        private static void Section(string name, Action check)
        {
            Console.WriteLine("== " + name);
            try
            {
                check();
            }
            catch (Exception error)
            {
                Fail(name + " threw " + error);
            }
        }

        private static void Expect(bool ok, string what)
        {
            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
            if (!ok)
            {
                _failures++;
            }
        }

        private static void Fail(string what) => Expect(false, what);

        private static string Write(string root, string relative, int bytes, DateTime? written = null)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[bytes]);
            File.SetLastWriteTimeUtc(path, written ?? DateTime.UtcNow.AddDays(-10));
            return path;
        }

        private static void FakeMachine(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            var c = Path.Combine(root, "C");
            var folders = new KnownFolders(c, Path.Combine(c, "Windows"), Path.Combine(c, "ProgramData"), Path.Combine(c, "Program Files (x86)"),
                Path.Combine(c, @"Users\ban"), Path.Combine(c, @"Users\ban\AppData\Local"), Path.Combine(c, @"Users\ban\AppData\Roaming"), Path.Combine(c, @"Users\ban\AppData\Local\Temp"));
            var chrome = @"C\Users\ban\AppData\Local\Google\Chrome\User Data\";
            Write(root, chrome + @"Default\Preferences", 10);
            var login = Write(root, chrome + @"Default\Login Data", 500);
            var history = Write(root, chrome + @"Default\History", 500);
            Write(root, chrome + @"Default\Cache\Cache_Data\f_000001", 40_000);
            var oldTemp = Write(root, @"C\Users\ban\AppData\Local\Temp\old.tmp", 3000);
            var newTemp = Write(root, @"C\Users\ban\AppData\Local\Temp\fresh.tmp", 3000, DateTime.UtcNow);
            var document = Write(root, @"C\Users\ban\Documents\thesis.docx", 9000);
            Write(root, @"C\Windows\Temp\setup.log", 2000);
            var system = Write(root, @"C\Windows\System32\config\SYSTEM", 2000);

            // A junction from the temp folder into Documents: cleaning must not walk through it.
            var link = Path.Combine(root, @"C\Users\ban\AppData\Local\Temp\docs");
            var junction = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{Path.Combine(root, @"C\Users\ban\Documents")}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            junction.WaitForExit();
            Expect(Directory.Exists(link), "junction created in Temp");

            var cleaned = CleanResult.None;
            foreach (var target in TargetCatalog.Build(folders).Where(t => t.Kind == TargetKind.Files))
            {
                var scan = CleanupScanner.Scan(target, folders, DateTime.UtcNow, elevated: true);
                cleaned = cleaned.Add(new Cleaner().Clean(scan, folders));
            }

            Console.WriteLine($"  cleaned {cleaned.Deleted} files, {cleaned.FreedBytes} bytes");
            Expect(cleaned.FreedBytes == 40_000 + 3000 + 2000, "freed exactly the cache, the old temp file and Windows\\Temp");
            Expect(!File.Exists(oldTemp) && File.Exists(newTemp), "temp: old file gone, file from today kept");
            Expect(File.Exists(login) && File.Exists(history), "Chrome passwords and history untouched");
            Expect(File.Exists(document), "file behind the junction untouched");
            Expect(File.Exists(system), "Windows\\System32 untouched");
            Expect(Directory.Exists(Path.Combine(root, chrome + @"Default\Cache")), "cache folder itself kept");
        }

        private static void ScanThisMachine()
        {
            var folders = WindowsFolders.Current();
            var service = new CleanService(folders, Elevation.IsElevated);
            Console.WriteLine($"  elevated {Elevation.IsElevated}; temp {folders.Temp}");
            long total = 0;
            foreach (var target in service.Targets)
            {
                var result = service.Scan(target, CancellationToken.None);
                total += result.Bytes;
                Console.WriteLine($"  {target.Id,-22} {Sizes.Format(result.Bytes),10}  {result.Items,7} item(s){(result.Blocked ? "  needs admin" : string.Empty)}{(result.AppRunning ? "  app running" : string.Empty)}");
            }

            Expect(service.Targets.Any(t => t.Id == "temp") && service.Targets.Any(t => t.Id == "update-cache"), "catalog has the Windows targets");
            Expect(service.Targets.SelectMany(t => t.Roots).All(r => PathGuard.IsSafeRoot(r, folders)), "every root on this machine passes PathGuard");
            Console.WriteLine("  total cleanable: " + Sizes.Format(total));
        }

        private static void LargeAndDuplicates(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            var same = Enumerable.Range(0, 300_000).Select(i => (byte)(i * 7)).ToArray();
            Directory.CreateDirectory(Path.Combine(root, "a"));
            Directory.CreateDirectory(Path.Combine(root, "b"));
            File.WriteAllBytes(Path.Combine(root, @"a\video.mp4"), same);
            File.WriteAllBytes(Path.Combine(root, @"b\video (copy).mp4"), same);
            File.WriteAllBytes(Path.Combine(root, @"b\other.bin"), new byte[300_000]);

            var large = LargeFileFinder.Find(root, 2, 0);
            Expect(large.Files.Count == 2 && large.FilesSeen == 3, "large files: top 2 of 3");
            var groups = DuplicateFinder.Find(new FileWalker().Walk(root, Array.Empty<string>(), true), 1);
            Expect(groups.Count == 1 && groups[0].Files.Count == 2, "duplicates: the two identical videos");
        }

        private static void InstalledAppsCheck()
        {
            var apps = InstalledApps.List();
            Console.WriteLine($"  {apps.Count} apps; biggest: " + string.Join(", ", apps.Where(a => a.Bytes.HasValue).OrderByDescending(a => a.Bytes).Take(5).Select(a => $"{a.Name} ({Sizes.Format(a.Bytes!.Value)})")));
            Expect(apps.Count > 0, "installed apps listed");
        }

        private static void StartupCheck()
        {
            const string name = "CleoraSelfTest";
            using (var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                run!.SetValue(name, "\"C:\\Windows\\notepad.exe\"");
            }

            try
            {
                var item = StartupApps.List().FirstOrDefault(i => i.Name == name);
                Expect(item != null && item.Enabled, "own Run entry listed as on");
                Expect(item != null && StartupApps.SetEnabled(item, false), "switched off");
                Expect(StartupApps.List().FirstOrDefault(i => i.Name == name)?.Enabled == false, "reads back as off");
                Expect(item != null && StartupApps.SetEnabled(item, true), "switched on");
                Expect(StartupApps.List().FirstOrDefault(i => i.Name == name)?.Enabled == true, "reads back as on");
                Console.WriteLine($"  {StartupApps.List().Count} startup item(s) on this machine");
            }
            finally
            {
                using (var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    run!.DeleteValue(name, throwOnMissingValue: false);
                }

                using (var approved = Registry.CurrentUser.CreateSubKey(StartupApproved.SubKey(StartupLocation.UserRun)))
                {
                    approved!.DeleteValue(name, throwOnMissingValue: false);
                }
            }
        }

        private static void RecycleCheck(string root)
        {
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "to-recycle.bin");
            File.WriteAllBytes(file, new byte[123_456]);
            var before = RecycleBin.Query();
            RecycleBin.Send(file);
            var after = RecycleBin.Query();
            Console.WriteLine($"  bin before {before.Bytes} B / {before.Items}, after {after.Bytes} B / {after.Items}");
            Expect(!File.Exists(file), "file left its folder");
            Expect(after.Items >= before.Items + 1, "Recycle Bin holds it");
            Expect(RecycleBin.Empty(), "Recycle Bin emptied");
            Expect(RecycleBin.Query().Items == 0, "Recycle Bin reads empty");
        }
    }
}
