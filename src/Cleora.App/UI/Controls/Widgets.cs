using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Cleora.Core;
using Cleora.Core.Animation;

namespace Cleora.App.UI.Controls
{
    internal abstract class Widget : Control
    {
        protected Widget()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Theme.Surface;
        }

        protected float Dpi => DeviceDpi / 96f * Theme.UiScale;

        protected int S(float value) => (int)Math.Round(value * Dpi);
    }

    internal enum RowState
    {
        Idle,
        Scanning,
        Found,
        Cleaning,
        Cleaned,
    }

    /// <summary>
    /// One place to clean: a check box, what it is, a one-line explanation, and the size found on the right, with a
    /// warning line when it needs administrator rights or an app is holding its files open.
    /// </summary>
    internal sealed class TargetRow : PaintedButton
    {
        private readonly Tween _check;
        private bool _checked;
        private string _size = string.Empty;
        private string? _warning;
        private RowState _state;

        public TargetRow(string id, string name, string detail, bool isChecked)
        {
            Id = id;
            Text = name;
            Detail = detail;
            _checked = isChecked;
            _check = new Tween(isChecked ? 1 : 0);
            AccessibleRole = AccessibleRole.CheckButton;
            Height = 62;
        }

        public event EventHandler? CheckedChanged;

        public string Id { get; }

        public string Detail { get; }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value)
                {
                    return;
                }

                _checked = value;
                AccessibleDescription = value ? "Đã chọn" : "Bỏ chọn";
                Animator.Animate(this, _check, value ? 1 : 0, 160);
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Show(RowState state, string size, string? warning)
        {
            _state = state;
            _size = size;
            _warning = warning;
            Height = (int)((warning == null ? 62 : 82) * Dpi);
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var on = (float)_check.Value;
            if (HoverAmount > 0.01f)
            {
                Theme.FillRounded(g, Theme.Fade(Theme.AccentHover, HoverAmount), RectangleF.Inflate(b, -1, -1), 8 * Dpi);
            }

            var box = new RectangleF(b.X + 12 * Dpi, b.Y + 14 * Dpi, 20 * Dpi, 20 * Dpi);
            Theme.FillRounded(g, Theme.Blend(Theme.Surface, Theme.AccentStrong, on), box, 5 * Dpi);
            Theme.StrokeRounded(g, Theme.Blend(Theme.Border, Theme.AccentStrong, on), 1.4f * Dpi, box, 5 * Dpi);
            if (on > 0.05f)
            {
                Icons.Draw(g, IconKind.Check, RectangleF.Inflate(box, -3 * Dpi, -3 * Dpi), Theme.Fade(Color.White, on));
            }

            var sizeColor = _state == RowState.Cleaned ? Theme.Ok : _state == RowState.Found && Checked ? Theme.Accent : Theme.Muted;
            var sizeFont = _state == RowState.Found || _state == RowState.Cleaned ? Theme.BodyBold : Theme.Small;
            var sizeWidth = Math.Max((int)(110 * Dpi), TextRenderer.MeasureText(_size, sizeFont).Width + (int)(8 * Dpi));
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var left = (int)(b.X + 46 * Dpi);
            var textWidth = (int)(b.Right - left - sizeWidth - 18 * Dpi);
            TextRenderer.DrawText(g, Text, Theme.BodyBold, new Rectangle(left, (int)(b.Y + 11 * Dpi), textWidth, (int)(22 * Dpi)), Theme.Text, flags);
            TextRenderer.DrawText(g, Detail, Theme.Small, new Rectangle(left, (int)(b.Y + 33 * Dpi), textWidth, (int)(20 * Dpi)), Theme.Muted, flags);
            if (_warning != null)
            {
                TextRenderer.DrawText(g, _warning, Theme.Small, new Rectangle(left, (int)(b.Y + 54 * Dpi), textWidth, (int)(20 * Dpi)), Theme.Pause, flags);
            }

            TextRenderer.DrawText(g, _size, sizeFont, new Rectangle((int)(b.Right - sizeWidth - 12 * Dpi), (int)(b.Y + 11 * Dpi), sizeWidth, (int)(24 * Dpi)), sizeColor,
                flags | TextFormatFlags.Right);
        }
    }

    /// <summary>A drive's space at a glance: big free-space number, a used bar, and how much Cleora has freed so far.</summary>
    internal sealed class DriveSummary : Widget
    {
        private readonly Tween _fill = new Tween(0);
        private string _title = string.Empty;
        private string _detail = string.Empty;
        private float _used;
        private bool _low;

        public DriveSummary()
        {
            Height = 96;
            AccessibleRole = AccessibleRole.ProgressBar;
        }

        public void Show(string title, string detail, float used, bool low)
        {
            _title = title;
            _detail = detail;
            _used = Math.Max(0, Math.Min(1, used));
            _low = low;
            AccessibleName = title + ". " + detail;
            Animator.Animate(this, _fill, _used, 600);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, _title, Theme.Title, new Rectangle(0, 0, Width, S(30)), Theme.Text, flags);
            TextRenderer.DrawText(g, _detail, Theme.Body, new Rectangle(0, S(34), Width, S(22)), Theme.Muted, flags);

            var bar = new RectangleF(0, S(66), Width - 1, S(12));
            Theme.FillRounded(g, Theme.AccentSoft, bar, bar.Height / 2);
            var used = new RectangleF(bar.X, bar.Y, Math.Max(bar.Height, bar.Width * (float)_fill.Value), bar.Height);
            Theme.FillRounded(g, _low ? Theme.Record : Theme.AccentStrong, used, bar.Height / 2);
        }
    }

    /// <summary>The biggest folders as horizontal bars, largest first.</summary>
    internal sealed class FolderBars : Widget
    {
        private IReadOnlyList<(string Name, long Bytes)> _rows = Array.Empty<(string, long)>();

        public FolderBars()
        {
            Height = 40;
        }

        public void Show(IEnumerable<(string Name, long Bytes)> rows)
        {
            _rows = rows.Take(8).ToList();
            Height = Math.Max(S(24), _rows.Count * S(34));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            if (_rows.Count == 0)
            {
                TextRenderer.DrawText(g, "Chưa quét.", Theme.Small, new Point(0, 0), Theme.Muted);
                return;
            }

            var max = Math.Max(1, _rows.Max(r => r.Bytes));
            var labelWidth = Math.Min(S(240), Width / 3);
            var sizeWidth = S(84);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
            for (var i = 0; i < _rows.Count; i++)
            {
                var top = i * S(34);
                TextRenderer.DrawText(g, _rows[i].Name, Theme.Body, new Rectangle(0, top, labelWidth - S(10), S(26)), Theme.Text, flags);
                var track = new RectangleF(labelWidth, top + S(8), Width - labelWidth - sizeWidth - S(10), S(10));
                Theme.FillRounded(g, Theme.AccentSoft, track, track.Height / 2);
                var fill = new RectangleF(track.X, track.Y, Math.Max(track.Height, track.Width * _rows[i].Bytes / max), track.Height);
                Theme.FillRounded(g, Theme.Blend(Theme.AccentStrong, Theme.Accent, i / 8.0), fill, track.Height / 2);
                TextRenderer.DrawText(g, Sizes.Format(_rows[i].Bytes), Theme.BodyBold, new Rectangle(Width - sizeWidth, top, sizeWidth, S(26)), Theme.Text, flags | TextFormatFlags.Right);
            }
        }
    }

    /// <summary>A details ListView in the palette's colours: owner-drawn header, quiet borders.</summary>
    internal static class Lists
    {
        public static ListView Create(params (string Title, int Width, bool Right)[] columns)
        {
            var list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                Font = Theme.Body,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Height = 320,
                Margin = new Padding(0, 0, 0, 12),
                OwnerDraw = true,
            };

            foreach (var column in columns)
            {
                list.Columns.Add(column.Title, column.Width, column.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left);
            }

            list.DrawItem += (_, e) => e.DrawDefault = true;
            list.DrawSubItem += (_, e) => e.DrawDefault = true;
            list.DrawColumnHeader += (_, e) => DrawHeader(e);
            return list;
        }

        private static void DrawHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (var fill = new SolidBrush(Theme.Background))
            using (var line = new Pen(Theme.Border))
            {
                e.Graphics.FillRectangle(fill, e.Bounds);
                e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
            }

            var align = e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.Small, Rectangle.Inflate(e.Bounds, -6, 0), Theme.Muted,
                align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
}
