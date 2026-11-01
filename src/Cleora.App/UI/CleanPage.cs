using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Cleora.App.UI.Controls;
using Cleora.Core.Cleanup;

namespace Cleora.App.UI
{
    /// <summary>
    /// The home page: how full the system drive is, one button to scan, then every place junk lives with what it
    /// holds, ticked by default only where cleaning is harmless.
    /// </summary>
    internal sealed class CleanPage : Page
    {
        private readonly Dictionary<string, TargetRow> _rows = new Dictionary<string, TargetRow>();
        private readonly List<Card> _groups = new List<Card>();
        private readonly Label _summary;
        private readonly Label _adminNote;

        public CleanPage(IReadOnlyList<CleanupTarget> targets, Func<string, bool, bool> isChosen, bool elevated)
            : base("Dọn rác", "Quét các chỗ Windows, trình duyệt và app để lại file thừa. Bạn chọn, Cleora mới xoá.")
        {
            var hero = AddCard(string.Empty);
            Drive = new DriveSummary { Width = 600, Margin = new Padding(0, 0, 0, 14) };
            hero.Body.Controls.Add(Drive);
            Scan = new FlatButton("Quét") { Primary = true, Size = new Size(150, 40), Margin = new Padding(0, 0, 10, 0) };
            Clean = new FlatButton("Dọn") { Primary = true, Size = new Size(190, 40), Margin = new Padding(0, 0, 10, 0), Enabled = false };
            _summary = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, BackColor = Theme.Surface, Margin = new Padding(6, 11, 0, 0) };
            hero.Body.Controls.Add(Row(Scan, Clean, _summary));

            _adminNote = Caption("Vài mục của Windows cần quyền quản trị. Mở lại Cleora với quyền quản trị để dọn cả những mục đó.", Theme.Small, Theme.Muted, 6);
            Elevate = new FlatButton("Mở lại với quyền quản trị") { Size = new Size(240, 34), Margin = new Padding(0, 0, 0, 2) };
            if (!elevated && targets.Any(t => t.Roots.Any(r => r.RequiresAdmin)))
            {
                hero.Body.Controls.Add(_adminNote);
                hero.Body.Controls.Add(Elevate);
            }

            foreach (var group in new[] { (TargetGroup.System, "Windows"), (TargetGroup.Browser, "Trình duyệt"), (TargetGroup.App, "Ứng dụng") })
            {
                var inGroup = targets.Where(t => t.Group == group.Item1).ToList();
                if (inGroup.Count == 0)
                {
                    continue;
                }

                var card = AddCard(group.Item2);
                _groups.Add(card);
                foreach (var target in inGroup)
                {
                    var row = new TargetRow(target.Id, target.Name, target.Detail, isChosen(target.Id, target.DefaultOn)) { Width = 600, Margin = new Padding(0, 0, 0, 2) };
                    row.CheckedChanged += (_, _) =>
                    {
                        ChoiceChanged?.Invoke(this, row);
                        UpdateTotal();
                    };
                    row.Show(RowState.Idle, string.Empty, null);
                    _rows[target.Id] = row;
                    card.Body.Controls.Add(row);
                }
            }
        }

        public event EventHandler<TargetRow>? ChoiceChanged;

        public DriveSummary Drive { get; }

        public FlatButton Scan { get; }

        public FlatButton Clean { get; }

        public FlatButton Elevate { get; }

        public IReadOnlyDictionary<string, TargetRow> Rows => _rows;

        /// <summary>Bytes found per target id, for the "Dọn 3,4 GB" total.</summary>
        public Dictionary<string, long> Found { get; } = new Dictionary<string, long>();

        public void ShowRow(string id, RowState state, string size, string? warning)
        {
            if (_rows.TryGetValue(id, out var row))
            {
                row.Show(state, size, warning);
                RefitCards();
            }
        }

        public void ShowSummary(string text) => _summary.Text = text;

        public long ChosenBytes => Found.Where(f => _rows.TryGetValue(f.Key, out var row) && row.Checked).Sum(f => f.Value);

        public void UpdateTotal()
        {
            var total = ChosenBytes;
            Clean.Text = total > 0 ? "Dọn " + Core.Sizes.Format(total) : "Dọn";
            Clean.Enabled = total > 0 && Scan.Enabled;
        }

        protected override void FitContent(int bodyWidth)
        {
            if (Drive == null)
            {
                return;
            }

            Drive.Width = bodyWidth;
            foreach (var row in _rows.Values)
            {
                row.Width = bodyWidth;
            }
        }
    }
}
