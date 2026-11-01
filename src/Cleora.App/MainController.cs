using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cleora.App.Cleaning;
using Cleora.App.Logging;
using Cleora.App.Platform;
using Cleora.App.UI;
using Cleora.App.UI.Controls;
using Cleora.Core;
using Cleora.Core.Cleanup;
using Cleora.Core.Scanning;
using Cleora.Core.Settings;
using Cleora.Core.Startup;
using Cleora.Core.Updates;

namespace Cleora.App
{
    /// <summary>Wires the pages to the scanners and cleaners; long work runs on the pool, the window stays live.</summary>
    internal sealed class MainController : IDisposable
    {
        private const int LargeFilesShown = 200;

        private readonly MainForm _form;
        private readonly AppSettings _settings;
        private readonly string _settingsPath;
        private readonly KnownFolders _folders;
        private readonly bool _elevated;
        private readonly CleanService _clean;
        private readonly Dictionary<string, TargetResult> _results = new Dictionary<string, TargetResult>();
        private readonly CleanPage _cleanPage;
        private readonly LargeFilesPage _largePage;
        private readonly DuplicatesPage _duplicatesPage;
        private readonly AppsPage _appsPage;
        private readonly StartupPage _startupPage;
        private readonly SettingsPage _settingsPage;
        private string[] _duplicateFolders;
        private bool _busy;
        private bool _appsLoaded;
        private bool _startupLoaded;
        private bool _disposed;
        private bool _updating;
        private AvailableUpdate? _update;

        public MainController(MainForm form, AppSettings settings, string settingsPath)
        {
            _form = form;
            _settings = settings;
            _settingsPath = settingsPath;
            _folders = WindowsFolders.Current();
            _elevated = Elevation.IsElevated;
            _clean = new CleanService(_folders, _elevated);
            _duplicateFolders = WindowsFolders.DuplicateDefaults();
            Log.Info($"elevated: {_elevated}; {_clean.Targets.Count} targets");

            _cleanPage = new CleanPage(_clean.Targets, settings.IsChosen, _elevated);
            _largePage = new LargeFilesPage(FixedDrives());
            _duplicatesPage = new DuplicatesPage(DescribeFolders(_duplicateFolders), settings.DuplicateMinMb, settings.KeepRule);
            _appsPage = new AppsPage();
            _startupPage = new StartupPage(_elevated);
            _settingsPage = new SettingsPage(Program.Version, settings.Theme, settings.FreedTotal);

            form.SetPage(PageKind.Clean, _cleanPage);
            form.SetPage(PageKind.LargeFiles, _largePage);
            form.SetPage(PageKind.Duplicates, _duplicatesPage);
            form.SetPage(PageKind.Apps, _appsPage);
            form.SetPage(PageKind.Startup, _startupPage);
            form.SetPage(PageKind.Settings, _settingsPage);

            _cleanPage.Scan.Click += (_, _) => ScanAll();
            _cleanPage.Clean.Click += (_, _) => CleanChosen();
            _cleanPage.Elevate.Click += (_, _) => RestartElevated();
            _cleanPage.ChoiceChanged += (_, row) =>
            {
                _settings.Choices[row.Id] = row.Checked;
                Save();
            };

            _largePage.Scan.Click += (_, _) => ScanLargeFiles();
            _largePage.Reveal.Click += (_, _) => RevealSelected(_largePage.List);
            _largePage.Delete.Click += (_, _) => RecycleLargeFiles();
            _duplicatesPage.ChangeFolder.Click += (_, _) => ChooseDuplicateFolder();
            _duplicatesPage.Scan.Click += (_, _) => ScanDuplicates();
            _duplicatesPage.Delete.Click += (_, _) => RecycleDuplicates();
            _duplicatesPage.MinSize.SelectedChanged += (_, _) =>
            {
                _settings.DuplicateMinMb = _duplicatesPage.MinSize.Value;
                Save();
            };
            _duplicatesPage.Keep.SelectedChanged += (_, _) =>
            {
                _settings.KeepRule = _duplicatesPage.Keep.Value;
                Save();
            };
            _appsPage.Reload.Click += (_, _) => LoadApps();
            _appsPage.Uninstall.Click += (_, _) => Uninstall();
            _startupPage.Toggled += (_, change) => ToggleStartup(change.Item, change.Enabled);
            _settingsPage.ThemeChoice.SelectedChanged += (_, _) =>
            {
                _settings.Theme = _settingsPage.ThemeChoice.Value;
                Save();
                ApplyTheme();
            };
            _settingsPage.CheckUpdate.Click += (_, _) => CheckUpdate();
            _settingsPage.UpdateNow.Click += (_, _) => UpdateNow();

            form.PageShown += (_, page) =>
            {
                if (page == PageKind.Apps && !_appsLoaded)
                {
                    LoadApps();
                }
                else if (page == PageKind.Startup && !_startupLoaded)
                {
                    LoadStartup();
                }
            };

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            Context = new ApplicationContext(form);
        }

        public ApplicationContext Context { get; }

        public void Start()
        {
            _form.ShowPage(PageKind.Clean);
            ShowDrive();
            _form.Status.SetState(_elevated ? "Sẵn sàng · quyền quản trị" : "Sẵn sàng", Theme.Ok);
            _form.Status.Details = "Bấm Quét để xem có thể dọn bao nhiêu";
            _form.Show();
            Updater.CleanUp();
            CheckForUpdateLater();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        internal static IReadOnlyList<(string Root, string Label)> FixedDrives() =>
            DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => (d.RootDirectory.FullName, $"{d.Name.TrimEnd('\\')} còn {Sizes.Format(d.AvailableFreeSpace)}"))
                .ToList();

        private void ShowDrive()
        {
            try
            {
                var drive = new DriveInfo(_folders.SystemDrive);
                var used = drive.TotalSize - drive.TotalFreeSpace;
                var freed = _settings.FreedTotal > 0 ? " · Cleora đã dọn " + Sizes.Format(_settings.FreedTotal) : string.Empty;
                _cleanPage.Drive.Show(
                    $"Ổ {drive.Name.TrimEnd('\\')} còn trống {Sizes.Format(drive.AvailableFreeSpace)}",
                    $"Đã dùng {Sizes.Format(used)} trên {Sizes.Format(drive.TotalSize)}{freed}",
                    drive.TotalSize > 0 ? (float)used / drive.TotalSize : 0,
                    drive.TotalSize > 0 && drive.AvailableFreeSpace < drive.TotalSize / 10);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                Log.Warn("drive info: " + error.Message);
            }
        }

        private async void ScanAll()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true, "Đang quét…");
            _results.Clear();
            _cleanPage.Found.Clear();
            foreach (var target in _clean.Targets)
            {
                _cleanPage.ShowRow(target.Id, RowState.Scanning, "Đang quét…", null);
            }

            try
            {
                foreach (var target in _clean.Targets)
                {
                    var result = await Task.Run(() => _clean.Scan(target, CancellationToken.None));
                    _results[target.Id] = result;
                    _cleanPage.Found[target.Id] = result.Bytes;
                    _cleanPage.ShowRow(target.Id, RowState.Found, RowSize(result), Warning(result));
                    _cleanPage.UpdateTotal();
                }
            }
            finally
            {
                SetBusy(false, null);
            }

            var total = _results.Values.Sum(r => r.Bytes);
            _cleanPage.ShowSummary("Tìm thấy " + Sizes.Format(total) + " có thể dọn");
            _form.Status.Details = "Đã quét xong: " + Sizes.Format(total) + " có thể dọn";
            _cleanPage.UpdateTotal();
            Log.Info("scan: " + string.Join(", ", _results.Values.Select(r => $"{r.Target.Id}={r.Bytes}")));
        }

        private static string RowSize(TargetResult result) =>
            result.Blocked ? "—" : result.Bytes > 0 || result.Items > 0 ? Sizes.Format(result.Bytes) : "Trống";

        private static string? Warning(TargetResult result)
        {
            if (result.Blocked)
            {
                return "Cần quyền quản trị: mở lại Cleora với quyền quản trị để dọn mục này";
            }

            if (result.AppRunning)
            {
                return "App đang mở nên vài tệp bị giữ lại. Đóng app trước khi dọn để dọn được hết";
            }

            return result.NeedsAdminRoots > 0 ? "Một phần cần quyền quản trị, Cleora chỉ dọn phần của bạn" : null;
        }

        private async void CleanChosen()
        {
            var chosen = _results.Values
                .Where(r => _cleanPage.Rows.TryGetValue(r.Target.Id, out var row) && row.Checked && !r.Blocked && (r.Bytes > 0 || r.Items > 0))
                .ToList();
            if (chosen.Count == 0 || _busy)
            {
                return;
            }

            var total = chosen.Sum(r => r.Bytes);
            var risky = chosen.Any(r => r.Target.Kind == TargetKind.RecycleBin || r.Target.Kind == TargetKind.WindowsOld);
            var question = $"Xoá hẳn {Sizes.Format(total)} ở {chosen.Count} mục đã chọn?"
                + (risky ? "\n\nCó cả Thùng rác hoặc bản Windows cũ: xoá rồi không lấy lại được." : "\n\nĐây là file tạm và bộ nhớ đệm, app sẽ tự tạo lại khi cần.");
            if (MessageBox.Show(_form, question, "Cleora", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true, "Đang dọn…");
            var sum = CleanResult.None;
            try
            {
                foreach (var result in chosen)
                {
                    _cleanPage.ShowRow(result.Target.Id, RowState.Cleaning, "Đang dọn…", null);
                    var done = await Task.Run(() => _clean.Clean(result, CancellationToken.None));
                    sum = sum.Add(done);
                    Log.Info($"cleaned {result.Target.Id}: {done.Deleted} files, {done.FreedBytes} bytes, {done.InUse} in use");
                    _cleanPage.Found[result.Target.Id] = 0;
                    _cleanPage.ShowRow(result.Target.Id, RowState.Cleaned, "Đã dọn " + Sizes.Format(done.FreedBytes),
                        done.InUse > 0 ? $"{done.InUse} tệp đang được dùng nên để lại" : null);
                }
            }
            finally
            {
                SetBusy(false, null);
            }

            _settings.FreedTotal += sum.FreedBytes;
            Save();
            ShowDrive();
            _settingsPage.Freed.Text = "Đã dọn trên máy này: " + Sizes.Format(_settings.FreedTotal);
            _cleanPage.ShowSummary("Đã dọn " + Sizes.Format(sum.FreedBytes));
            _cleanPage.UpdateTotal();
            _form.Status.Details = $"Đã dọn {Sizes.Format(sum.FreedBytes)} ({sum.Deleted} tệp)";
        }

        private async void ScanLargeFiles()
        {
            if (_busy)
            {
                return;
            }

            var root = _largePage.Drives.Value;
            SetBusy(true, "Đang đọc ổ " + root + "…");
            var progress = new Progress<int>(seen => _largePage.ShowStatus($"Đã xem {seen:N0} tệp…"));
            try
            {
                var system = _folders.SystemAreas;
                var scan = await Task.Run(() => LargeFileFinder.Find(root, LargeFilesShown, 1 << 20, progress,
                    listable: path => !system.Any(area => PathGuard.IsInside(path, area))));
                _largePage.ShowResult(scan);
                _largePage.ShowStatus($"{scan.FilesSeen:N0} tệp, {Sizes.Format(scan.TotalBytes)}" + (scan.SkippedFolders > 0 ? $" · {scan.SkippedFolders} thư mục không đọc được" : string.Empty));
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void RecycleLargeFiles()
        {
            var items = _largePage.List.SelectedItems.Cast<ListViewItem>().ToList();
            if (items.Count == 0)
            {
                return;
            }

            var files = items.Select(i => (FoundFile)i.Tag).ToList();
            var question = files.Count == 1
                ? $"Chuyển \"{Path.GetFileName(files[0].Path)}\" ({Sizes.Format(files[0].Length)}) vào Thùng rác?"
                : $"Chuyển {files.Count} tệp ({Sizes.Format(files.Sum(f => f.Length))}) vào Thùng rác?";
            if (MessageBox.Show(_form, question, "Cleora", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            foreach (var item in items)
            {
                if (Recycle(((FoundFile)item.Tag).Path))
                {
                    _largePage.List.Items.Remove(item);
                }
            }
        }

        private void ChooseDuplicateFolder()
        {
            using (var dialog = new FolderBrowserDialog { SelectedPath = _duplicateFolders.FirstOrDefault() ?? _folders.UserProfile, Description = "Chọn thư mục để tìm file trùng" })
            {
                if (dialog.ShowDialog(_form) == DialogResult.OK)
                {
                    _duplicateFolders = new[] { dialog.SelectedPath };
                    _duplicatesPage.ShowFolder(DescribeFolders(_duplicateFolders));
                }
            }
        }

        private async void ScanDuplicates()
        {
            if (_busy)
            {
                return;
            }

            var folders = _duplicateFolders;
            var minBytes = (long)_settings.DuplicateMinMb << 20;
            var rule = _settings.KeepRule;
            SetBusy(true, "Đang tìm file trùng…");
            _duplicatesPage.ShowStatus("Đang liệt kê tệp…");
            var progress = new Progress<int>(done => _duplicatesPage.ShowStatus($"Đã so {done:N0} tệp cùng cỡ…"));
            try
            {
                var groups = await Task.Run(() =>
                {
                    var walker = new FileWalker();
                    var files = folders
                        .SelectMany(f => walker.Walk(f, Array.Empty<string>(), recursive: true, skipFolder: SkipForDuplicates))
                        .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();
                    return DuplicateFinder.Find(files, Math.Max(1, minBytes), progress: progress);
                });
                _duplicatesPage.ShowGroups(groups, rule);
                _duplicatesPage.ShowStatus(groups.Count == 0
                    ? "Không có file trùng."
                    : $"{groups.Count} nhóm, thừa {Sizes.Format(groups.Sum(g => g.Wasted))}");
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void RecycleDuplicates()
        {
            var ticked = _duplicatesPage.List.CheckedItems.Cast<ListViewItem>().Select(i => ((DuplicateGroup Group, FoundFile File))i.Tag).ToList();
            var deletable = ticked
                .GroupBy(t => t.Group)
                .SelectMany(g => DuplicateFinder.Deletable(g.Key, new HashSet<string>(g.Select(t => t.File.Path), StringComparer.OrdinalIgnoreCase), _settings.KeepRule, Unchanged))
                .ToList();
            if (deletable.Count == 0)
            {
                return;
            }

            if (MessageBox.Show(_form, $"Chuyển {deletable.Count} bản thừa ({Sizes.Format(deletable.Sum(f => f.Length))}) vào Thùng rác? Mỗi nhóm luôn giữ lại ít nhất một bản.",
                    "Cleora", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            var gone = new HashSet<string>(deletable.Where(f => Recycle(f.Path)).Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
            foreach (var item in _duplicatesPage.List.Items.Cast<ListViewItem>().Where(i => gone.Contains((((DuplicateGroup, FoundFile File))i.Tag).File.Path)).ToList())
            {
                _duplicatesPage.List.Items.Remove(item);
            }

            _duplicatesPage.ShowStatus($"Đã chuyển {gone.Count} bản thừa vào Thùng rác");
        }

        /// <summary>Still on disk exactly as scanned; a copy changed or moved since is neither deleted nor counted as kept.</summary>
        private static bool Unchanged(FoundFile file)
        {
            try
            {
                var info = new FileInfo(file.Path);
                return info.Exists && info.Length == file.Length && info.LastWriteTimeUtc == file.LastWriteUtc;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                return false;
            }
        }

        private static bool SkipForDuplicates(DirectoryInfo folder)
        {
            try
            {
                return DuplicateFinder.SkipFolder(folder.Name, (folder.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static string DescribeFolders(string[] folders) =>
            folders.Length == 1 ? folders[0] : string.Join(", ", folders.Select(Path.GetFileName)) + " (bỏ qua thư mục ẩn và dữ liệu app)";

        private bool Recycle(string path)
        {
            if (!RecycleBin.OnFixedDrive(path))
            {
                MessageBox.Show(_form, "File nằm trên ổ rời hoặc ổ mạng, không có Thùng rác nên Cleora không xoá. Hãy tự xoá nếu chắc chắn.", "Cleora", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            try
            {
                var recycled = RecycleBin.Send(path);
                Log.Info((recycled ? "recycled " : "not recycled ") + path);
                return recycled;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is OperationCanceledException)
            {
                Log.Warn($"recycle {path}: {error.Message}");
                return false;
            }
        }

        private static void RevealSelected(ListView list)
        {
            if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is FoundFile file)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + file.Path + "\"") { UseShellExecute = true });
            }
        }

        private async void LoadApps()
        {
            _appsLoaded = true;
            var apps = await Task.Run(InstalledApps.List);
            _appsPage.ShowApps(apps);
        }

        private void Uninstall()
        {
            var app = _appsPage.Selected;
            if (app == null)
            {
                return;
            }

            if (MessageBox.Show(_form, $"Mở bộ gỡ của \"{app.Name}\"? Làm theo hướng dẫn của bộ gỡ, xong bấm Tải lại.", "Cleora",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes && !InstalledApps.Start(app))
            {
                MessageBox.Show(_form, "Không mở được bộ gỡ của app này. Thử gỡ trong Cài đặt Windows → Ứng dụng.", "Cleora", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void LoadStartup()
        {
            _startupLoaded = true;
            var items = await Task.Run(StartupApps.List);
            _startupPage.ShowItems(items);
        }

        private void ToggleStartup(StartupItem item, bool enabled)
        {
            if (!StartupApps.SetEnabled(item, enabled))
            {
                MessageBox.Show(_form, "Không đổi được mục này. Mục của mọi người dùng cần quyền quản trị.", "Cleora", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadStartup();
            }
        }

        private async void CheckUpdate()
        {
            _settingsPage.ShowUpdate("Đang kiểm tra…");
            var result = await UpdateService.CheckAsync();
            if (result.Update != null)
            {
                _update = result.Update;
                _settingsPage.OfferUpdate(result.Update.Version);
            }
            else
            {
                _settingsPage.ShowUpdate(result.Failed ? "Không kiểm tra được, xem mạng rồi thử lại." : "Bạn đang dùng bản mới nhất.");
            }
        }

        /// <summary>One quiet check a few seconds after start; a newer release puts a dot on Cài đặt and its button there.</summary>
        private async void CheckForUpdateLater()
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            var update = (await UpdateService.CheckAsync()).Update;
            if (update != null && !_disposed)
            {
                _update = update;
                _settingsPage.OfferUpdate(update.Version);
                _form.Status.Details = $"Có Cleora bản {update.Version.ToString(3)}: vào Cài đặt bấm Cập nhật";
            }
        }

        /// <summary>Downloads the new installer, checks its SHA-256, runs it silently and closes so it can replace Cleora.</summary>
        private async void UpdateNow()
        {
            var update = _update;
            if (update == null || _updating)
            {
                return;
            }

            if (!Updater.CanUpdateInPlace || !update.CanInstall)
            {
                Process.Start(new ProcessStartInfo(SettingsPage.Homepage) { UseShellExecute = true });
                return;
            }

            if (_busy)
            {
                MessageBox.Show(_form, "Cleora đang quét hoặc dọn. Đợi xong rồi bấm Cập nhật.", "Cleora", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _updating = true;
            _settingsPage.UpdateNow.Enabled = false;
            try
            {
                var page = _settingsPage;
                var installer = await Task.Run(() => Updater.Download(update, (done, total) =>
                {
                    if (total > 0)
                    {
                        page.BeginInvoke(new Action(() => page.ShowUpdate($"Đang tải bản {update.Version.ToString(3)}: {done * 100 / total}%")));
                    }
                }, CancellationToken.None));
                _settingsPage.ShowUpdate("Đang cài bản mới, Cleora sẽ tự mở lại…");
                Updater.Install(installer);
                _form.Close();
            }
            catch (Exception error) when (error is CleoraException || error is System.Net.WebException || error is IOException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception)
            {
                Log.Error("update failed", error);
                _settingsPage.ShowUpdate("Không cập nhật được, xem mạng rồi bấm lại.");
                _settingsPage.UpdateNow.Enabled = true;
                _updating = false;
            }
        }

        private void RestartElevated()
        {
            if (Elevation.RestartElevated("--after-elevate"))
            {
                Log.Info("restarting elevated");
                _form.Close();
            }
        }

        private void SetBusy(bool busy, string? state)
        {
            _busy = busy;
            _cleanPage.Scan.Enabled = !busy;
            _largePage.Scan.Enabled = !busy;
            _duplicatesPage.Scan.Enabled = !busy;
            _form.Status.SetState(state ?? (_elevated ? "Sẵn sàng · quyền quản trị" : "Sẵn sàng"), busy ? Theme.Pause : Theme.Ok);
            _cleanPage.UpdateTotal();
        }

        private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && _settings.Theme == ThemeMode.System && !_disposed && _form.IsHandleCreated)
            {
                _form.BeginInvoke(new Action(ApplyTheme));
            }
        }

        private void ApplyTheme()
        {
            var palette = Theme.For(_settings.Theme);
            if (!ReferenceEquals(palette, Theme.Current))
            {
                Theme.Use(palette, _form);
            }
        }

        private void Save()
        {
            try
            {
                SettingsStore.Save(_settingsPath, _settings);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn("settings not saved: " + error.Message);
            }
        }
    }
}
