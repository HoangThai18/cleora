using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cleora.Core.Cleanup
{
    /// <summary>
    /// Every place Cleora cleans, found on this machine. Browsers and apps that aren't installed are left out.
    /// Only caches: passwords, history, cookies, sessions and downloads are never in a root (BR-02).
    /// </summary>
    public static class TargetCatalog
    {
        private static readonly TimeSpan Day = TimeSpan.FromHours(24);

        /// <summary>Cache folders inside a Chromium profile; anything else in the profile is the user's data.</summary>
        private static readonly string[] ChromiumProfileCaches = { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache" };

        private static readonly string[] ChromiumSharedCaches = { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" };

        private static readonly string[] ElectronCaches = { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache" };

        public static IReadOnlyList<CleanupTarget> Build(KnownFolders f)
        {
            var targets = new List<CleanupTarget>();
            targets.AddRange(System(f));
            targets.AddRange(Browsers(f));
            targets.AddRange(Apps(f));
            return targets.Where(t => t.Kind != TargetKind.Files || t.Roots.Count > 0).ToList();
        }

        private static IEnumerable<CleanupTarget> System(KnownFolders f)
        {
            string Win(params string[] parts) => Path.Combine(new[] { f.Windows }.Concat(parts).ToArray());
            string Local(params string[] parts) => Path.Combine(new[] { f.LocalAppData }.Concat(parts).ToArray());

            // TEMP comes from the environment: only a folder that really is a temp folder is emptied.
            var tempName = Path.GetFileName(f.Temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var tempRoots = string.Equals(tempName, "Temp", StringComparison.OrdinalIgnoreCase) || string.Equals(tempName, "Tmp", StringComparison.OrdinalIgnoreCase)
                ? new[] { CleanupRoot.All(f.Temp) }
                : new CleanupRoot[0];
            yield return Target("temp", TargetGroup.System, "File tạm của bạn",
                "Các app và bộ cài để lại trong thư mục Temp. Chỉ xoá file đã cũ hơn một ngày.",
                Day, true, null, tempRoots);
            yield return Target("windows-temp", TargetGroup.System, "File tạm của Windows",
                "Thư mục Windows\\Temp. Chỉ xoá file đã cũ hơn một ngày.",
                Day, true, null, CleanupRoot.All(Win("Temp"), admin: true));
            yield return Target("update-cache", TargetGroup.System, "Bản tải Windows Update",
                "Gói cập nhật đã cài xong vẫn nằm lại trên ổ. Windows tự tải lại nếu cần.",
                TimeSpan.Zero, true, null, CleanupRoot.All(Win("SoftwareDistribution", "Download"), admin: true));
            yield return Target("delivery-optimization", TargetGroup.System, "Bộ nhớ chia sẻ cập nhật",
                "Bản cập nhật Windows giữ lại để chia cho máy khác (Delivery Optimization).",
                TimeSpan.Zero, true, null,
                CleanupRoot.All(Win("ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"), admin: true),
                CleanupRoot.All(Win("SoftwareDistribution", "DeliveryOptimization"), admin: true));
            yield return Target("error-reports", TargetGroup.System, "Báo lỗi Windows",
                "Báo cáo sự cố app và Windows đã gửi hoặc không cần gửi nữa.",
                TimeSpan.Zero, true, null,
                CleanupRoot.All(Local("Microsoft", "Windows", "WER", "ReportArchive")),
                CleanupRoot.All(Local("Microsoft", "Windows", "WER", "ReportQueue")),
                CleanupRoot.All(Path.Combine(f.ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"), admin: true),
                CleanupRoot.All(Path.Combine(f.ProgramData, "Microsoft", "Windows", "WER", "ReportQueue"), admin: true));
            yield return Target("crash-dumps", TargetGroup.System, "Bản ghi lúc sập",
                "File dump khi app hoặc Windows bị sập (màn hình xanh). Chỉ thợ sửa máy mới cần.",
                TimeSpan.Zero, true, null,
                CleanupRoot.All(Local("CrashDumps")),
                CleanupRoot.All(Win("Minidump"), admin: true),
                CleanupRoot.Only(f.Windows, true, "MEMORY.DMP"));
            yield return Target("thumbnails", TargetGroup.System, "Ảnh thu nhỏ trong Explorer",
                "Windows tạo lại khi bạn mở thư mục. Tệp đang dùng được bỏ qua.",
                TimeSpan.Zero, true, null,
                CleanupRoot.Only(Local("Microsoft", "Windows", "Explorer"), false, "thumbcache_*.db", "iconcache_*.db"));
            yield return Target("windows-logs", TargetGroup.System, "Nhật ký cài đặt Windows",
                "Nhật ký cài đặt và sửa lỗi (CBS, DISM) đã cũ hơn một tuần.",
                TimeSpan.FromDays(7), true, null,
                CleanupRoot.All(Win("Logs", "CBS"), admin: true),
                CleanupRoot.All(Win("Logs", "DISM"), admin: true));
            yield return Target("shader-cache", TargetGroup.System, "Bộ nhớ đệm đồ hoạ (shader)",
                "DirectX, NVIDIA, AMD, Intel. Dọn xong, game lần đầu mở lại có thể giật nhẹ vài phút khi dựng lại.",
                TimeSpan.Zero, false, null,
                CleanupRoot.All(Local("D3DSCache")),
                CleanupRoot.All(Local("NVIDIA", "DXCache")),
                CleanupRoot.All(Local("NVIDIA", "GLCache")),
                CleanupRoot.All(Local("AMD", "DxCache")),
                CleanupRoot.All(Local("AMD", "DxcCache")),
                CleanupRoot.All(Local("AMD", "GLCache")),
                CleanupRoot.All(Local("Intel", "ShaderCache")));
            yield return new CleanupTarget("recycle-bin", TargetGroup.System, "Thùng rác",
                "Mọi file trong Thùng rác của các ổ. Xoá rồi là không lấy lại được.",
                Array.Empty<CleanupRoot>(), TimeSpan.Zero, false, TargetKind.RecycleBin);
            yield return new CleanupTarget("windows-old", TargetGroup.System, "Bản Windows cũ (Windows.old)",
                "Windows giữ lại sau khi nâng cấp để quay về bản cũ. Xoá rồi thì không quay về được nữa.",
                new[] { CleanupRoot.All(Path.Combine(f.SystemDrive, "Windows.old"), admin: true) }, TimeSpan.Zero, false, TargetKind.WindowsOld);
        }

        private static IEnumerable<CleanupTarget> Browsers(KnownFolders f)
        {
            var chromium = new (string Id, string Name, string Folder, string Process)[]
            {
                ("chrome", "Google Chrome", Path.Combine(f.LocalAppData, "Google", "Chrome", "User Data"), "chrome"),
                ("edge", "Microsoft Edge", Path.Combine(f.LocalAppData, "Microsoft", "Edge", "User Data"), "msedge"),
                ("coccoc", "Cốc Cốc", Path.Combine(f.LocalAppData, "CocCoc", "Browser", "User Data"), "browser"),
                ("brave", "Brave", Path.Combine(f.LocalAppData, "BraveSoftware", "Brave-Browser", "User Data"), "brave"),
                ("vivaldi", "Vivaldi", Path.Combine(f.LocalAppData, "Vivaldi", "User Data"), "vivaldi"),
                ("opera", "Opera", Path.Combine(f.LocalAppData, "Opera Software"), "opera"),
            };

            foreach (var browser in chromium)
            {
                var roots = new List<CleanupRoot>();
                foreach (var profile in ChromiumProfiles(browser.Folder))
                {
                    roots.AddRange(ChromiumProfileCaches.Select(c => Path.Combine(profile, c)).Where(Directory.Exists).Select(p => CleanupRoot.All(p)));
                }

                roots.AddRange(ChromiumSharedCaches.Select(c => Path.Combine(browser.Folder, c)).Where(Directory.Exists).Select(p => CleanupRoot.All(p)));
                yield return Target(browser.Id, TargetGroup.Browser, "Bộ nhớ đệm " + browser.Name,
                    "Ảnh, mã trang web đã tải. Không đụng mật khẩu, lịch sử, cookie hay tab đang mở.",
                    TimeSpan.Zero, true, new[] { browser.Process }, roots.ToArray());
            }

            var firefox = Path.Combine(f.LocalAppData, "Mozilla", "Firefox", "Profiles");
            var foxRoots = SubFolders(firefox)
                .SelectMany(p => new[] { "cache2", "startupCache", "thumbnails", "jumpListCache" }.Select(c => Path.Combine(p, c)))
                .Where(Directory.Exists)
                .Select(p => CleanupRoot.All(p))
                .ToArray();
            yield return Target("firefox", TargetGroup.Browser, "Bộ nhớ đệm Firefox",
                "Ảnh, mã trang web đã tải. Không đụng mật khẩu, lịch sử, cookie hay tab đang mở.",
                TimeSpan.Zero, true, new[] { "firefox" }, foxRoots);
        }

        private static IEnumerable<CleanupTarget> Apps(KnownFolders f)
        {
            var electron = new (string Id, string Name, string[] Folders, string Process)[]
            {
                ("discord", "Discord", new[] { Path.Combine(f.RoamingAppData, "discord"), Path.Combine(f.RoamingAppData, "discordptb"), Path.Combine(f.RoamingAppData, "discordcanary") }, "Discord"),
                ("zalo", "Zalo", new[] { Path.Combine(f.RoamingAppData, "ZaloPC"), Path.Combine(f.RoamingAppData, "Zalo") }, "Zalo"),
                ("teams", "Microsoft Teams (cổ điển)", new[] { Path.Combine(f.RoamingAppData, "Microsoft", "Teams") }, "Teams"),
                ("slack", "Slack", new[] { Path.Combine(f.RoamingAppData, "Slack") }, "slack"),
                ("vscode", "Visual Studio Code", new[] { Path.Combine(f.RoamingAppData, "Code") }, "Code"),
                ("messenger", "Messenger", new[] { Path.Combine(f.RoamingAppData, "Messenger") }, "Messenger"),
            };

            foreach (var app in electron)
            {
                var roots = app.Folders
                    .SelectMany(folder => ElectronCaches.Select(c => Path.Combine(folder, c)))
                    .Where(Directory.Exists)
                    .Select(p => CleanupRoot.All(p))
                    .ToArray();
                yield return Target(app.Id, TargetGroup.App, "Bộ nhớ đệm " + app.Name,
                    "Ảnh, video xem trước và mã giao diện tải về. Tin nhắn, file đã nhận và đăng nhập giữ nguyên.",
                    TimeSpan.Zero, true, new[] { app.Process }, roots);
            }

            var steam = new[]
            {
                Path.Combine(f.LocalAppData, "Steam", "htmlcache"),
                Path.Combine(f.ProgramFilesX86, "Steam", "appcache", "httpcache"),
                Path.Combine(f.ProgramFilesX86, "Steam", "depotcache"),
            }.Where(Directory.Exists).Select(p => CleanupRoot.All(p, admin: p.StartsWith(f.ProgramFilesX86, StringComparison.OrdinalIgnoreCase))).ToArray();
            yield return Target("steam", TargetGroup.App, "Bộ nhớ đệm Steam",
                "Trang cửa hàng, ảnh và gói tải dở. Game đã cài giữ nguyên.",
                TimeSpan.Zero, true, new[] { "steam", "steamwebhelper" }, steam);
        }

        /// <summary>
        /// Profile folders of a Chromium browser: those holding a Preferences file, or a Cache folder (Opera keeps
        /// Preferences in Roaming and only the cache under Local).
        /// </summary>
        internal static IEnumerable<string> ChromiumProfiles(string userData)
        {
            bool IsProfile(string folder) => File.Exists(Path.Combine(folder, "Preferences")) || Directory.Exists(Path.Combine(folder, "Cache"));

            if (IsProfile(userData))
            {
                yield return userData;
            }

            foreach (var folder in SubFolders(userData))
            {
                if (IsProfile(folder))
                {
                    yield return folder;
                }
            }
        }

        private static IEnumerable<string> SubFolders(string path)
        {
            try
            {
                return Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static CleanupTarget Target(string id, TargetGroup group, string name, string detail, TimeSpan minAge, bool on, string[]? processes, params CleanupRoot[] roots) =>
            new CleanupTarget(id, group, name, detail, roots, minAge, on, TargetKind.Files, processes);
    }
}
