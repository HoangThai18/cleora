using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Cleora.App.UI.Controls;
using Cleora.Core;
using Cleora.Core.Apps;
using Cleora.Core.Cleanup;
using Cleora.Core.Scanning;
using Cleora.Core.Settings;
using Cleora.Core.Startup;

namespace Cleora.App.UI
{
    /// <summary>
    /// <c>Cleora.exe --ui-screenshot out.png [--ui-scale 1.5] [--page Clean] [--theme dark]</c>: renders a page filled
    /// with sample results to a PNG and exits, so every page can be reviewed without a Windows PC.
    /// </summary>
    internal static class UiScreenshot
    {
        public static int Run(string path, float scale, PageKind page, string? theme)
        {
            Theme.Configure(scale);
            Theme.Use(string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? Palette.Dark : Palette.Light);
            Animator.Off = true;

            var root = @"C:\Users\ban\AppData\Local";
            var folders = new KnownFolders(@"C:\", @"C:\Windows", @"C:\ProgramData", @"C:\Program Files (x86)", @"C:\Users\ban", root, @"C:\Users\ban\AppData\Roaming", root + @"\Temp");
            var targets = TargetCatalog.Build(folders).ToList();
            targets.AddRange(new[]
            {
                Sample("chrome", TargetGroup.Browser, "Bộ nhớ đệm Google Chrome", "Ảnh, mã trang web đã tải. Không đụng mật khẩu, lịch sử, cookie hay tab đang mở."),
                Sample("coccoc", TargetGroup.Browser, "Bộ nhớ đệm Cốc Cốc", "Ảnh, mã trang web đã tải. Không đụng mật khẩu, lịch sử, cookie hay tab đang mở."),
                Sample("discord", TargetGroup.App, "Bộ nhớ đệm Discord", "Ảnh, video xem trước và mã giao diện tải về. Tin nhắn, file đã nhận và đăng nhập giữ nguyên."),
                Sample("zalo", TargetGroup.App, "Bộ nhớ đệm Zalo", "Ảnh, video xem trước và mã giao diện tải về. Tin nhắn, file đã nhận và đăng nhập giữ nguyên."),
                Sample("steam", TargetGroup.App, "Bộ nhớ đệm Steam", "Trang cửa hàng, ảnh và gói tải dở. Game đã cài giữ nguyên."),
            });

            using (var form = new MainForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.ShowInTaskbar = false;

                var clean = new CleanPage(targets, (_, on) => on, elevated: false);
                var large = new LargeFilesPage(new[] { (@"C:\", "C: còn 41,8 GB"), (@"D:\", "D: còn 212,4 GB") });
                var duplicates = new DuplicatesPage(@"C:\Users\ban", 1, KeepRule.Newest);
                var apps = new AppsPage();
                var startup = new StartupPage(elevated: false);
                var settings = new SettingsPage(Program.Version, ThemeMode.System, 18_600_000_000);
                form.SetPage(PageKind.Clean, clean);
                form.SetPage(PageKind.LargeFiles, large);
                form.SetPage(PageKind.Duplicates, duplicates);
                form.SetPage(PageKind.Apps, apps);
                form.SetPage(PageKind.Startup, startup);
                form.SetPage(PageKind.Settings, settings);
                form.Show();
                form.ShowPage(page);

                clean.Drive.Show("Ổ C: còn trống 41,8 GB", "Đã dùng 196,6 GB trên 238,4 GB · Cleora đã dọn 17,3 GB", 0.82f, false);
                var sizes = new (string Id, long Bytes)[]
                {
                    ("temp", 1_850_000_000), ("windows-temp", 0), ("update-cache", 3_420_000_000), ("delivery-optimization", 0), ("error-reports", 96_000_000),
                    ("crash-dumps", 0), ("thumbnails", 210_000_000), ("windows-logs", 0), ("shader-cache", 1_120_000_000), ("recycle-bin", 4_700_000_000),
                    ("windows-old", 0), ("chrome", 2_310_000_000), ("coccoc", 640_000_000), ("discord", 980_000_000), ("zalo", 1_540_000_000), ("steam", 410_000_000),
                };
                foreach (var size in sizes)
                {
                    clean.Found[size.Id] = size.Bytes;
                    var admin = size.Id == "windows-temp" || size.Id == "update-cache" || size.Id == "delivery-optimization" || size.Id == "windows-logs" || size.Id == "windows-old";
                    clean.ShowRow(size.Id, admin ? RowState.Found : RowState.Found, admin ? "—" : size.Bytes > 0 ? Sizes.Format(size.Bytes) : "Trống",
                        admin ? "Cần quyền quản trị: mở lại Cleora với quyền quản trị để dọn mục này"
                        : size.Id == "chrome" ? "App đang mở nên vài tệp bị giữ lại. Đóng app trước khi dọn để dọn được hết" : null);
                    if (admin)
                    {
                        clean.Found[size.Id] = 0;
                    }
                }

                clean.ShowSummary("Tìm thấy 13,3 GB có thể dọn");
                clean.UpdateTotal();

                var files = new[]
                {
                    new FoundFile(@"D:\Games\Delta Force\Saved\Paks\pakchunk2.pak", 18_400_000_000, DateTime.Now.AddDays(-3)),
                    new FoundFile(@"C:\Users\ban\Videos\Recora\Recora 2026-10-28 21-04-11.mp4", 6_700_000_000, DateTime.Now.AddDays(-4)),
                    new FoundFile(@"C:\hiberfil.sys", 6_400_000_000, DateTime.Now),
                    new FoundFile(@"C:\Users\ban\Downloads\Windows11_24H2.iso", 5_900_000_000, DateTime.Now.AddDays(-60)),
                    new FoundFile(@"C:\Users\ban\Downloads\Adobe Premiere Pro 2025.zip", 3_100_000_000, DateTime.Now.AddDays(-90)),
                };
                large.ShowResult(new LargeScan(files, new[]
                {
                    new FolderSize(@"C:\Users", 88_000_000_000, 210_000), new FolderSize(@"C:\Windows", 31_000_000_000, 160_000),
                    new FolderSize(@"C:\Program Files", 24_000_000_000, 90_000), new FolderSize(@"C:\Program Files (x86)", 19_000_000_000, 70_000),
                    new FolderSize(@"C:\ProgramData", 7_000_000_000, 40_000),
                }, 196_600_000_000, 612_345, 12));
                large.ShowStatus("612.345 tệp, 196,6 GB");

                var photo = new DuplicateGroup(4_200_000, "a", new[]
                {
                    new FoundFile(@"C:\Users\ban\Pictures\Đà Lạt 2025\IMG_2041.jpg", 4_200_000, DateTime.Now.AddDays(-200)),
                    new FoundFile(@"C:\Users\ban\Downloads\IMG_2041.jpg", 4_200_000, DateTime.Now.AddDays(-20)),
                });
                var video = new DuplicateGroup(1_480_000_000, "b", new[]
                {
                    new FoundFile(@"C:\Users\ban\Videos\Recora\Recora 2026-10-20 khoảnh khắc.mp4", 1_480_000_000, DateTime.Now.AddDays(-10)),
                    new FoundFile(@"C:\Users\ban\Desktop\clip hay.mp4", 1_480_000_000, DateTime.Now.AddDays(-9)),
                    new FoundFile(@"D:\Backup\clip hay.mp4", 1_480_000_000, DateTime.Now.AddDays(-2)),
                });
                duplicates.ShowGroups(new[] { video, photo }, KeepRule.Newest);
                duplicates.ShowStatus("2 nhóm, thừa 2,8 GB");

                apps.ShowApps(new[]
                {
                    new InstalledApp("Delta Force", "TiMi Studio Group", "1.0", 92_000_000_000, DateTime.Now.AddDays(-30), "u", "k"),
                    new InstalledApp("League of Legends", "Riot Games, Inc", "14.21", 31_000_000_000, DateTime.Now.AddDays(-400), "u", "k"),
                    new InstalledApp("Adobe Premiere Pro 2025", "Adobe Inc.", "25.0", 9_800_000_000, DateTime.Now.AddDays(-90), "u", "k"),
                    new InstalledApp("Zalo", "VNG Corp.", "24.10.1", 820_000_000, DateTime.Now.AddDays(-12), "u", "k"),
                    new InstalledApp("Google Chrome", "Google LLC", "130.0", 640_000_000, DateTime.Now.AddDays(-5), "u", "k"),
                    new InstalledApp("Recora", "thaipro.store", "0.3.0", 21_000_000, DateTime.Now, "u", "k"),
                    new InstalledApp("UniKey", "Phạm Kim Long", "4.6", null, null, "u", "k"),
                });

                startup.ShowItems(new[]
                {
                    new StartupItem("Discord", @"C:\Users\ban\AppData\Local\Discord\Update.exe --processStart Discord.exe", StartupLocation.UserRun, true),
                    new StartupItem("Steam", "\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent", StartupLocation.UserRun, false),
                    new StartupItem("Zalo", @"C:\Users\ban\AppData\Local\Programs\Zalo\Zalo.exe --startup", StartupLocation.UserRun, true),
                    new StartupItem("SecurityHealth", @"%windir%\system32\SecurityHealthSystray.exe", StartupLocation.MachineRun, true),
                    new StartupItem("Recora.lnk", @"C:\Users\ban\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Recora.lnk", StartupLocation.UserFolder, false),
                });

                form.Status.SetState("Sẵn sàng", Theme.Ok);
                form.Status.Details = "Đã quét xong: 13,3 GB có thể dọn";
                for (var i = 0; i < 10; i++)
                {
                    Application.DoEvents();
                }

                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(path, ImageFormat.Png);
                }

                Console.WriteLine($"saved {path} ({form.Width}x{form.Height}, scale {scale}, page {page}, theme {theme ?? "light"})");
            }

            return 0;
        }

        private static CleanupTarget Sample(string id, TargetGroup group, string name, string detail) =>
            new CleanupTarget(id, group, name, detail, new[] { CleanupRoot.All(@"C:\Users\ban\AppData\Local\" + id + @"\Cache") }, TimeSpan.Zero, true);
    }
}
