using System;
using System.Drawing;
using System.Windows.Forms;
using Cleora.App.UI.Controls;

namespace Cleora.App.UI
{
    internal enum PageKind
    {
        Clean,
        LargeFiles,
        Duplicates,
        Apps,
        Startup,
        Settings,
    }

    /// <summary>A page in the content area: a title row, then cards stacked top to bottom with scrolling.</summary>
    internal class Page : Panel
    {
        public Page(string title, string? subtitle = null)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = true;
            Padding = new Padding(26, 20, 26, 20);

            Stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
            };

            Stack.Controls.Add(new Label
            {
                Text = title,
                Font = Theme.Title,
                ForeColor = Theme.Text,
                BackColor = Theme.Background,
                AutoSize = true,
                Margin = new Padding(2, 0, 0, subtitle == null ? 14 : 2),
            });

            if (subtitle != null)
            {
                Stack.Controls.Add(new Label
                {
                    Text = subtitle,
                    Font = Theme.Body,
                    ForeColor = Theme.Muted,
                    BackColor = Theme.Background,
                    AutoSize = true,
                    Margin = new Padding(2, 0, 0, 16),
                });
            }

            Controls.Add(Stack);
            Resize += (_, _) => FitCards();
            VisibleChanged += (_, _) =>
            {
                if (Visible)
                {
                    FitCards();
                }
            };
        }

        protected FlowLayoutPanel Stack { get; }

        protected float Dpi => DeviceDpi / 96f * Theme.UiScale;

        /// <summary>Adds a card; with no height it grows to fit its content.</summary>
        protected Card AddCard(string title, int height = 0)
        {
            var card = new Card(title) { Height = height > 0 ? height : 60, AutoHeight = height <= 0, Margin = new Padding(0, 0, 0, 14) };
            Stack.Controls.Add(card);
            FitCards();
            return card;
        }

        protected static Label Caption(string text, Font font, Color color, int bottom = 6) => new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, bottom),
            BackColor = Theme.Surface,
        };

        protected static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, 8) };
            row.Controls.AddRange(controls);
            return row;
        }

        protected void RefitCards() => FitCards();

        /// <summary>Lets a page stretch its rows and lists to the new card width before the cards re-measure.</summary>
        protected virtual void FitContent(int bodyWidth)
        {
        }

        private void FitCards()
        {
            var width = Math.Max(300, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            var body = width - (int)(40 * Dpi);
            FitContent(body);
            foreach (Control control in Stack.Controls)
            {
                if (control is Card card)
                {
                    card.Width = width;
                    if (card.AutoHeight)
                    {
                        card.FitHeight();
                    }
                }
            }
        }
    }
}
