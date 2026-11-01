using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Cleora.App.UI.Controls;
using Cleora.Core;
using Cleora.Core.Apps;
using Cleora.Core.Scanning;
using Cleora.Core.Startup;

namespace Cleora.App.UI
{
    /// <summary>Which folders and files take the most room on a drive; files go to the Recycle Bin, never straight away.</summary>
    internal sealed class LargeFilesPage : Page
    {
        private readonly Label _status;

        public LargeFilesPage(IReadOnlyList<(string Root, string Label)> drives)
            : base("File lớn", "Tìm thư mục và file chiếm nhiều chỗ nhất. File xoá ở đây vào Thùng rác, lấy lại được.")
        {
            var pick = AddCard("Ổ đĩa");
            Drives = new Segmented<string>(drives.Select(d => (d.Root, d.Label)), drives.FirstOrDefault().Root ?? @"C:\") { Width = Math.Max(160, drives.Count * 150) };
            Scan = new FlatButton("Quét ổ này") { Primary = true, Size = new Size(150, 38), Margin = new Padding(0, 0, 10, 0) };
            _status = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, BackColor = Theme.Surface, Margin = new Padding(6, 10, 0, 0) };
            pick.Body.Controls.Add(Row(Drives));
            pick.Body.Controls.Add(Row(Scan, _status));

            var folders = AddCard("Thư mục chiếm nhiều chỗ");
            Folders = new FolderBars { Width = 600 };
            folders.Body.Controls.Add(Folders);

            var files = AddCard("File lớn nhất");
            List = Lists.Create(("Tên", 280, false), ("Dung lượng", 90, true), ("Thư mục", 300, false), ("Sửa lần cuối", 110, false));
            List.MultiSelect = true;
            files.Body.Controls.Add(List);
            Reveal = new FlatButton("Mở thư mục chứa") { Size = new Size(170, 34), Margin = new Padding(0, 0, 10, 0) };
            Delete = new FlatButton("Xoá vào Thùng rác") { Size = new Size(180, 34), Margin = Padding.Empty };
            files.Body.Controls.Add(Row(Reveal, Delete));
        }

        public Segmented<string> Drives { get; }

        public FlatButton Scan { get; }

        public FolderBars Folders { get; }

        public ListView List { get; }

        public FlatButton Reveal { get; }

        public FlatButton Delete { get; }

        public void ShowStatus(string text) => _status.Text = text;

        public void ShowResult(LargeScan scan)
        {
            Folders.Show(scan.Folders.Select(f => (Path.GetFileName(f.Path.TrimEnd('\\')) is { Length: > 0 } name ? name : f.Path, f.Bytes)));
            List.BeginUpdate();
            List.Items.Clear();
            foreach (var file in scan.Files)
            {
                var item = new ListViewItem(new[] { Path.GetFileName(file.Path), Sizes.Format(file.Length), Path.GetDirectoryName(file.Path) ?? string.Empty, file.LastWriteUtc.ToLocalTime().ToString("dd/MM/yyyy") })
                {
                    Tag = file,
                };
                List.Items.Add(item);
            }

            List.EndUpdate();
            RefitCards();
        }

        protected override void FitContent(int bodyWidth)
        {
            if (Folders != null)
            {
                Folders.Width = bodyWidth;
                List.Width = bodyWidth;
                List.Columns[2].Width = Math.Max(160, bodyWidth - List.Columns[0].Width - List.Columns[1].Width - List.Columns[3].Width - 30);
            }
        }
    }

    /// <summary>Files with exactly the same content; every copy but one is ticked, and one copy always stays.</summary>
    internal sealed class DuplicatesPage : Page
    {
        private readonly Label _folder;
        private readonly Label _status;

        public DuplicatesPage(string folder, int minMb, KeepRule rule)
            : base("File trùng", "Tìm file giống hệt nhau (so cả nội dung, không chỉ tên). Bản thừa vào Thùng rác, luôn giữ lại một bản.")
        {
            var where = AddCard("Tìm ở đâu");
            _folder = Caption(folder, Theme.Body, Theme.Text, 8);
            where.Body.Controls.Add(_folder);
            ChangeFolder = new FlatButton("Đổi thư mục…") { Size = new Size(150, 34), Margin = new Padding(0, 0, 0, 12) };
            where.Body.Controls.Add(ChangeFolder);
            where.Body.Controls.Add(Caption("Bỏ qua file nhỏ hơn", Theme.BodyBold, Theme.Text, 6));
            MinSize = new Segmented<int>(new[] { (0, "Mọi cỡ"), (1, "1 MB"), (10, "10 MB"), (100, "100 MB") }, minMb) { Width = 420, Margin = new Padding(0, 0, 0, 12) };
            where.Body.Controls.Add(MinSize);
            where.Body.Controls.Add(Caption("Giữ lại bản", Theme.BodyBold, Theme.Text, 6));
            Keep = new Segmented<KeepRule>(new[] { (KeepRule.Newest, "Mới nhất"), (KeepRule.Oldest, "Cũ nhất"), (KeepRule.ShortestPath, "Đường dẫn ngắn nhất") }, rule) { Width = 460, Margin = new Padding(0, 0, 0, 14) };
            where.Body.Controls.Add(Keep);
            Scan = new FlatButton("Tìm file trùng") { Primary = true, Size = new Size(160, 38), Margin = new Padding(0, 0, 10, 0) };
            _status = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, BackColor = Theme.Surface, Margin = new Padding(6, 10, 0, 0) };
            where.Body.Controls.Add(Row(Scan, _status));

            var result = AddCard("Kết quả");
            List = Lists.Create(("Tệp", 320, false), ("Dung lượng", 90, true), ("Thư mục", 300, false), ("Sửa lần cuối", 110, false));
            List.CheckBoxes = true;
            List.Height = 360;
            result.Body.Controls.Add(List);
            Delete = new FlatButton("Xoá các bản đã chọn vào Thùng rác") { Size = new Size(300, 34), Margin = Padding.Empty, Enabled = false };
            result.Body.Controls.Add(Delete);
        }

        public FlatButton ChangeFolder { get; }

        public Segmented<int> MinSize { get; }

        public Segmented<KeepRule> Keep { get; }

        public FlatButton Scan { get; }

        public ListView List { get; }

        public FlatButton Delete { get; }

        public void ShowFolder(string path) => _folder.Text = path;

        public void ShowStatus(string text) => _status.Text = text;

        public void ShowGroups(IReadOnlyList<DuplicateGroup> groups, KeepRule rule)
        {
            List.BeginUpdate();
            List.Items.Clear();
            List.Groups.Clear();
            foreach (var group in groups.Take(500))
            {
                var header = new ListViewGroup($"{group.Files.Count} bản giống nhau · {Sizes.Format(group.Length)} mỗi bản · thừa {Sizes.Format(group.Wasted)}");
                List.Groups.Add(header);
                var keep = DuplicateFinder.Keep(group, rule);
                foreach (var file in group.Files)
                {
                    List.Items.Add(new ListViewItem(new[] { Path.GetFileName(file.Path), Sizes.Format(file.Length), Path.GetDirectoryName(file.Path) ?? string.Empty, file.LastWriteUtc.ToLocalTime().ToString("dd/MM/yyyy") }, header)
                    {
                        Tag = (group, file),
                        Checked = !ReferenceEquals(file, keep),
                    });
                }
            }

            List.EndUpdate();
            Delete.Enabled = groups.Count > 0;
            RefitCards();
        }

        protected override void FitContent(int bodyWidth)
        {
            if (List != null)
            {
                List.Width = bodyWidth;
                List.Columns[2].Width = Math.Max(160, bodyWidth - List.Columns[0].Width - List.Columns[1].Width - List.Columns[3].Width - 30);
            }
        }
    }

    /// <summary>Installed apps with their size, to remove the ones no longer used through their own uninstaller.</summary>
    internal sealed class AppsPage : Page
    {
        private readonly Label _summary;
        private IReadOnlyList<InstalledApp> _apps = Array.Empty<InstalledApp>();

        public AppsPage()
            : base("Gỡ ứng dụng", "Các app đã cài, app nặng nhất ở trên. Gỡ bằng bộ gỡ của chính app đó, như trong Cài đặt Windows.")
        {
            var card = AddCard(string.Empty);
            Search = new TextBox { Width = 320, Font = Theme.Body, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 4, 10, 0) };
            Reload = new FlatButton("Tải lại") { Size = new Size(110, 34), Margin = new Padding(0, 0, 10, 0) };
            _summary = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, BackColor = Theme.Surface, Margin = new Padding(6, 9, 0, 0) };
            card.Body.Controls.Add(Caption("Tìm app", Theme.BodyBold, Theme.Text, 6));
            card.Body.Controls.Add(Row(Search, Reload, _summary));
            List = Lists.Create(("Tên", 300, false), ("Nhà phát hành", 200, false), ("Dung lượng", 100, true), ("Ngày cài", 110, false));
            List.Height = 420;
            List.MultiSelect = false;
            card.Body.Controls.Add(List);
            Uninstall = new FlatButton("Gỡ app này") { Primary = true, Size = new Size(150, 36), Margin = Padding.Empty, Enabled = false };
            card.Body.Controls.Add(Uninstall);
            Search.TextChanged += (_, _) => Fill();
            List.SelectedIndexChanged += (_, _) => Uninstall.Enabled = List.SelectedItems.Count == 1;
        }

        public TextBox Search { get; }

        public FlatButton Reload { get; }

        public ListView List { get; }

        public FlatButton Uninstall { get; }

        public InstalledApp? Selected => List.SelectedItems.Count == 1 ? List.SelectedItems[0].Tag as InstalledApp : null;

        public void ShowApps(IReadOnlyList<InstalledApp> apps)
        {
            _apps = apps;
            var known = apps.Where(a => a.Bytes.HasValue).Sum(a => a.Bytes!.Value);
            _summary.Text = $"{apps.Count} app · khoảng {Sizes.Format(known)}";
            Fill();
        }

        protected override void FitContent(int bodyWidth)
        {
            if (List != null)
            {
                List.Width = bodyWidth;
                List.Columns[0].Width = Math.Max(200, bodyWidth - List.Columns[1].Width - List.Columns[2].Width - List.Columns[3].Width - 30);
            }
        }

        private void Fill()
        {
            var query = Search.Text.Trim();
            List.BeginUpdate();
            List.Items.Clear();
            foreach (var app in _apps
                .Where(a => query.Length == 0 || a.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || a.Publisher.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .OrderByDescending(a => a.Bytes ?? -1))
            {
                List.Items.Add(new ListViewItem(new[]
                {
                    app.Name,
                    app.Publisher,
                    app.Bytes.HasValue ? Sizes.Format(app.Bytes.Value) : "—",
                    app.Installed?.ToString("dd/MM/yyyy") ?? "—",
                })
                {
                    Tag = app,
                });
            }

            List.EndUpdate();
            Uninstall.Enabled = false;
        }
    }

    /// <summary>Apps that start with Windows, each with a switch (the same one Task Manager uses).</summary>
    internal sealed class StartupPage : Page
    {
        private readonly Card _list;
        private readonly bool _elevated;

        public StartupPage(bool elevated)
            : base("Khởi động", "App tự chạy khi mở máy làm máy khởi động chậm. Tắt ở đây giống tắt trong Task Manager, bật lại lúc nào cũng được.")
        {
            _elevated = elevated;
            _list = AddCard("Chạy cùng Windows");
        }

        public event EventHandler<(StartupItem Item, bool Enabled)>? Toggled;

        public void ShowItems(IReadOnlyList<StartupItem> items)
        {
            _list.Body.SuspendLayout();
            _list.Body.Controls.Clear();
            if (items.Count == 0)
            {
                _list.Body.Controls.Add(Caption("Không có app nào chạy cùng Windows.", Theme.Body, Theme.Muted, 4));
            }

            foreach (var item in items)
            {
                var toggle = new ToggleSwitch(item.Name, item.Enabled) { Size = new Size(520, 30), Margin = new Padding(0, 6, 0, 0) };
                var where = item.Location == StartupLocation.UserFolder || item.Location == StartupLocation.CommonFolder ? "Thư mục Startup" : "Registry";
                var locked = item.RequiresAdmin && !_elevated;
                toggle.Enabled = !locked;
                toggle.CheckedChanged += (_, _) => Toggled?.Invoke(this, (item, toggle.Checked));
                _list.Body.Controls.Add(toggle);
                _list.Body.Controls.Add(Caption(
                    (locked ? "Cần quyền quản trị · " : string.Empty) + where + " · " + Shorten(item.Command),
                    Theme.Small, Theme.Muted, 8));
            }

            _list.Body.ResumeLayout();
            RefitCards();
        }

        private static string Shorten(string command) => command.Length > 110 ? command.Substring(0, 107) + "…" : command;
    }
}
