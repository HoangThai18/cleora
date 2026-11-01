using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Cleora.App.UI
{
    internal enum IconKind
    {
        Sparkle,
        Drive,
        Copy,
        Grid,
        Power,
        Sliders,
        Info,
        Folder,
        Trash,
        Search,
        Shield,
        Check,
        Refresh,
    }

    /// <summary>
    /// Line icons drawn on a 24-unit grid (Lucide-style) and scaled into any box, so the exe needs no image files and
    /// stays sharp at every DPI.
    /// </summary>
    internal static class Icons
    {
        public static void Draw(Graphics g, IconKind kind, RectangleF box, Color color)
        {
            var size = Math.Min(box.Width, box.Height);
            var unit = size / 24f;
            var ox = box.X + (box.Width - size) / 2;
            var oy = box.Y + (box.Height - size) / 2;

            PointF P(float x, float y) => new PointF(ox + x * unit, oy + y * unit);
            RectangleF R(float x, float y, float w, float h) => new RectangleF(ox + x * unit, oy + y * unit, w * unit, h * unit);

            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(color, Math.Max(1.2f, 1.9f * unit)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, P(x1, y1), P(x2, y2));
                void Poly(params float[] xy)
                {
                    var points = new PointF[xy.Length / 2];
                    for (var i = 0; i < points.Length; i++)
                    {
                        points[i] = P(xy[i * 2], xy[i * 2 + 1]);
                    }

                    g.DrawLines(pen, points);
                }

                void Rounded(float x, float y, float w, float h, float r)
                {
                    using (var path = Theme.RoundedRect(R(x, y, w, h), r * unit))
                    {
                        g.DrawPath(pen, path);
                    }
                }

                void Circle(float cx, float cy, float r) => g.DrawEllipse(pen, R(cx - r, cy - r, r * 2, r * 2));
                void Dot(float cx, float cy, float r) => g.FillEllipse(brush, R(cx - r, cy - r, r * 2, r * 2));

                switch (kind)
                {
                    case IconKind.Sparkle:
                        // A four-point star with a small one beside it: "make it shine".
                        Poly(10, 3, 11.6f, 8.4f, 17, 10, 11.6f, 11.6f, 10, 17, 8.4f, 11.6f, 3, 10, 8.4f, 8.4f, 10, 3);
                        Line(18, 15, 18, 21);
                        Line(15, 18, 21, 18);
                        break;
                    case IconKind.Drive:
                        Rounded(2.5f, 13, 19, 7, 2);
                        Poly(2.5f, 15, 5.5f, 5, 18.5f, 5, 21.5f, 15);
                        Dot(17.5f, 16.5f, 1);
                        Line(6, 16.5f, 11, 16.5f);
                        break;
                    case IconKind.Copy:
                        Rounded(8, 8, 13, 13, 2);
                        Poly(16, 8, 16, 5, 14, 3, 5, 3, 3, 5, 3, 14, 5, 16, 8, 16);
                        break;
                    case IconKind.Grid:
                        Rounded(3, 3, 7.5f, 7.5f, 1.8f);
                        Rounded(13.5f, 3, 7.5f, 7.5f, 1.8f);
                        Rounded(3, 13.5f, 7.5f, 7.5f, 1.8f);
                        Rounded(13.5f, 13.5f, 7.5f, 7.5f, 1.8f);
                        break;
                    case IconKind.Power:
                        g.DrawArc(pen, R(4, 4.5f, 16, 16), -50, 280);
                        Line(12, 2.5f, 12, 11);
                        break;
                    case IconKind.Sliders:
                        Line(4, 7, 20, 7);
                        Line(4, 17, 20, 17);
                        Dot(9, 7, 2.7f);
                        Dot(15, 17, 2.7f);
                        break;
                    case IconKind.Info:
                        Circle(12, 12, 9.5f);
                        Line(12, 11, 12, 16.5f);
                        Dot(12, 7.8f, 1.1f);
                        break;
                    case IconKind.Folder:
                        Poly(3, 18.5f, 3, 5.5f, 9, 5.5f, 11, 8, 21, 8, 21, 18.5f, 3, 18.5f);
                        break;
                    case IconKind.Trash:
                        Line(3.5f, 6, 20.5f, 6);
                        Poly(9, 6, 9, 3.5f, 15, 3.5f, 15, 6);
                        Poly(5.5f, 6, 6.5f, 20.5f, 17.5f, 20.5f, 18.5f, 6);
                        Line(10, 10.5f, 10, 16.5f);
                        Line(14, 10.5f, 14, 16.5f);
                        break;
                    case IconKind.Search:
                        Circle(10.5f, 10.5f, 6.5f);
                        Line(15.5f, 15.5f, 20.5f, 20.5f);
                        break;
                    case IconKind.Shield:
                        Poly(12, 2.5f, 20, 5.5f, 20, 11.5f, 12, 21.5f, 4, 11.5f, 4, 5.5f, 12, 2.5f);
                        Poly(8.5f, 12, 11, 14.5f, 15.5f, 9.5f);
                        break;
                    case IconKind.Check:
                        Poly(4.5f, 12.5f, 9.5f, 17.5f, 19.5f, 6.5f);
                        break;
                    case IconKind.Refresh:
                        g.DrawArc(pen, R(4, 4, 16, 16), -60, 300);
                        Poly(17, 2.5f, 17.8f, 6.6f, 13.8f, 7.6f);
                        break;
                }
            }

            g.Restore(state);
        }
    }
}
