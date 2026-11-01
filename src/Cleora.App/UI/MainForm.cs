using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Cleora.App.UI.Controls;

namespace Cleora.App.UI
{
    /// <summary>The main window: pages on the left, the open page on the right, a status line at the bottom.</summary>
    internal sealed class MainForm : Form
    {
        private readonly Dictionary<PageKind, NavButton> _nav = new Dictionary<PageKind, NavButton>();
        private readonly Dictionary<PageKind, Page> _pages = new Dictionary<PageKind, Page>();
        private readonly Panel _content;
        private readonly PageFade _fade = new PageFade();

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f / Theme.UiScale, 96f / Theme.UiScale);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Cleora";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ClientSize = new Size(1000, 700);
            MinimumSize = new Size(840, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Large;

            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            Status = new AppStatusBar { Dock = DockStyle.Bottom, Height = 30 };
            Controls.Add(_content);
            _content.Controls.Add(_fade);
            Controls.Add(BuildSidebar());
            Controls.Add(Status);
            ResumeLayout(false);
            PerformLayout();
        }

        public AppStatusBar Status { get; }

        public PageKind CurrentPage { get; private set; } = (PageKind)(-1);

        public event EventHandler<PageKind>? PageShown;

        public void SetPage(PageKind kind, Page page)
        {
            if (_pages.TryGetValue(kind, out var old))
            {
                _content.Controls.Remove(old);
                old.Dispose();
            }

            var factor = DeviceDpi / 96f * Theme.UiScale;
            if (Math.Abs(factor - 1f) > 0.01f)
            {
                page.Scale(new SizeF(factor, factor));
            }

            page.Visible = kind == CurrentPage;
            _pages[kind] = page;
            _content.Controls.Add(page);
            Theme.StyleScrollBars(page);
        }

        public T? PageOf<T>(PageKind kind)
            where T : Page => _pages.TryGetValue(kind, out var page) ? page as T : null;

        public void ShowPage(PageKind kind)
        {
            var animate = kind != CurrentPage && Animator.Enabled && Visible && WindowState != FormWindowState.Minimized && IsHandleCreated;
            var before = animate ? PageFade.Snapshot(_content) : null;
            CurrentPage = kind;
            foreach (var pair in _pages)
            {
                pair.Value.Visible = pair.Key == kind;
            }

            foreach (var pair in _nav)
            {
                pair.Value.Selected = pair.Key == kind;
            }

            if (before != null)
            {
                _fade.BackColor = _content.BackColor;
                _fade.Play(_content, before);
            }

            PageShown?.Invoke(this, kind);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.StyleWindow(this);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Platform.SingleInstance.ShowMessage && m.Msg != 0)
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = FormWindowState.Normal;
                }

                Activate();
                return;
            }

            base.WndProc(ref m);
        }

        private Control BuildSidebar()
        {
            var host = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Theme.Sidebar };
            var brand = new BrandMark { Dock = DockStyle.Top, Height = 74 };
            var version = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Text = "Cleora " + Program.Version + " · miễn phí",
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                BackColor = Theme.Sidebar,
                Padding = new Padding(16, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var side = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Sidebar,
                Padding = new Padding(0, 4, 0, 0),
            };

            var items = new (PageKind Kind, IconKind Icon, string Text)[]
            {
                (PageKind.Clean, IconKind.Sparkle, "Dọn rác"),
                (PageKind.LargeFiles, IconKind.Drive, "File lớn"),
                (PageKind.Duplicates, IconKind.Copy, "File trùng"),
                (PageKind.Apps, IconKind.Grid, "Gỡ ứng dụng"),
                (PageKind.Startup, IconKind.Power, "Khởi động"),
                (PageKind.Settings, IconKind.Sliders, "Cài đặt"),
            };

            foreach (var item in items)
            {
                var button = new NavButton(item.Icon, item.Text) { Size = new Size(210, 44), Margin = Padding.Empty };
                button.Click += (_, _) => ShowPage(item.Kind);
                _nav[item.Kind] = button;
                side.Controls.Add(button);
            }

            host.Controls.Add(side);
            host.Controls.Add(brand);
            host.Controls.Add(version);
            return host;
        }

        /// <summary>The app icon and name at the top of the sidebar.</summary>
        private sealed class BrandMark : Control
        {
            public BrandMark()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Sidebar;
                AccessibleName = "Cleora";
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                var dpi = DeviceDpi / 96f * Theme.UiScale;
                var size = (int)(34 * dpi);
                var top = (Height - size) / 2;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(AppIcon.Bitmap, new Rectangle((int)(18 * dpi), top, size, size));
                TextRenderer.DrawText(g, "Cleora", Theme.Brand, new Rectangle((int)(18 * dpi) + size + (int)(10 * dpi), 0, Width, Height), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }
}
