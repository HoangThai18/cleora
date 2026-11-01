using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Cleora.App.Logging;
using Cleora.App.UI.Controls;
using Cleora.Core.Settings;

namespace Cleora.App.UI
{
    internal sealed class SettingsPage : Page
    {
        public const string Homepage = "https://thaipro.store/phan-mem/cleora";
        public const string SourceCode = "https://github.com/HoangThai18/cleora";

        private readonly Label _update;

        public SettingsPage(string version, ThemeMode theme, long freedTotal)
            : base("Cài đặt")
        {
            var look = AddCard("Giao diện");
            ThemeChoice = new Segmented<ThemeMode>(new[] { (ThemeMode.System, "Theo Windows"), (ThemeMode.Light, "Sáng"), (ThemeMode.Dark, "Tối") }, theme) { Width = 420, Margin = new Padding(0, 0, 0, 6) };
            look.Body.Controls.Add(ThemeChoice);
            look.Body.Controls.Add(Caption("Theo Windows: tự đổi sáng, tối cùng cài đặt màu của Windows.", UI.Theme.Small, UI.Theme.Muted, 4));

            var about = AddCard("Giới thiệu");
            about.Body.Controls.Add(Caption("Cleora " + version, UI.Theme.Title, UI.Theme.Accent, 4));
            about.Body.Controls.Add(Caption(
                "Dọn dung lượng miễn phí cho Windows: file tạm, cache Windows Update, cache trình duyệt và app, file lớn, file trùng, "
                + "gỡ app và tắt app khởi động. Chỉ xoá khi bạn bấm, không quảng cáo, không gửi dữ liệu đi đâu.",
                UI.Theme.Body, UI.Theme.Text, 8));
            Freed = Caption(freedTotal > 0 ? "Đã dọn trên máy này: " + Core.Sizes.Format(freedTotal) : string.Empty, UI.Theme.BodyBold, UI.Theme.Ok, 12);
            about.Body.Controls.Add(Freed);
            about.Body.Controls.Add(Links(("Trang tải", Homepage), ("Mã nguồn", SourceCode)));
            _update = Caption(string.Empty, UI.Theme.Small, UI.Theme.Muted, 0);
            CheckUpdate = new FlatButton("Kiểm tra bản mới") { Size = new Size(170, 34), Margin = new Padding(0, 12, 0, 4) };
            about.Body.Controls.Add(CheckUpdate);
            about.Body.Controls.Add(_update);

            var support = AddCard("Khi gặp lỗi");
            support.Body.Controls.Add(Caption(
                "Cleora ghi nhật ký trên máy để tìm lỗi: những gì đã xoá, mục nào bị bỏ qua. Gửi file nhật ký mới nhất khi báo lỗi.",
                UI.Theme.Body, UI.Theme.Text, 10));
            var logs = new FlatButton("Mở thư mục nhật ký") { Size = new Size(190, 34), Margin = Padding.Empty };
            logs.Click += (_, _) => OpenFolder(Log.DefaultDirectory);
            support.Body.Controls.Add(logs);

            var licenses = AddCard("Giấy phép");
            licenses.Body.Controls.Add(Caption("Cleora: giấy phép MIT, mã nguồn mở trên GitHub.", UI.Theme.Body, UI.Theme.Text, 4));
        }

        public Segmented<ThemeMode> ThemeChoice { get; }

        public Label Freed { get; }

        public FlatButton CheckUpdate { get; }

        public void ShowUpdate(string text) => _update.Text = text;

        private static FlowLayoutPanel Links(params (string Text, string Url)[] links)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = UI.Theme.Surface, Margin = Padding.Empty };
            foreach (var link in links)
            {
                var button = new FlatButton(link.Text) { Size = new Size(130, 34), Margin = new Padding(0, 0, 10, 0) };
                button.Click += (_, _) => Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
                row.Controls.Add(button);
            }

            return row;
        }

        private static void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
