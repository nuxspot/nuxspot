using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GranRuedaDorada.Rendering;

namespace GranRuedaDorada.Controls
{
    /// <summary>Red seven-segment LED meter with the unlit segments faintly visible, like a real credit meter.</summary>
    public sealed class LedDisplay : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(long), typeof(LedDisplay),
            new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DigitsProperty = DependencyProperty.Register(
            nameof(Digits), typeof(int), typeof(LedDisplay),
            new FrameworkPropertyMetadata(7, FrameworkPropertyMetadataOptions.AffectsRender));

        public long Value { get => (long)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public int Digits { get => (int)GetValue(DigitsProperty); set => SetValue(DigitsProperty, value); }

        private static readonly Brush Lit = Art.Vertical((0, "#FF7A66"), (0.5, "#FF2A1A"), (1, "#FF5040"));
        private static readonly Brush Unlit = Art.Solid("#2E0A08");

        // Segment bits: a b c d e f g
        private static readonly int[] Masks =
        {
            0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011,
            0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011,
        };

        public LedDisplay()
        {
            Effect = new DropShadowEffect { Color = Art.C("#FF3B2F"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.85 };
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double h = ActualHeight, w = h * 0.55, gap = h * 0.17, t = h * 0.13;
            if (h <= 0) return;
            string s = Math.Max(0, Value).ToString();
            int n = Math.Max(Digits, s.Length);
            double x = ActualWidth - n * (w + gap) + gap;
            dc.PushTransform(new SkewTransform(-7, 0, 0, h / 2));
            for (int i = 0; i < n; i++)
            {
                int idx = i - (n - s.Length);
                int mask = idx >= 0 ? Masks[s[idx] - '0'] : 0;
                DrawDigit(dc, x + i * (w + gap), w, h, t, mask);
            }
            dc.Pop();
        }

        private static void DrawDigit(DrawingContext dc, double x, double w, double h, double t, int mask)
        {
            double g = t * 0.18, ht = t / 2;
            var segs = new[]
            {
                Horizontal(x + ht + g, x + w - ht - g, ht),           // a
                Vertical(x + w - ht, ht + g, h / 2 - g),              // b
                Vertical(x + w - ht, h / 2 + g, h - ht - g),          // c
                Horizontal(x + ht + g, x + w - ht - g, h - ht),       // d
                Vertical(x + ht, h / 2 + g, h - ht - g),              // e
                Vertical(x + ht, ht + g, h / 2 - g),                  // f
                Horizontal(x + ht + g, x + w - ht - g, h / 2),        // g
            };
            for (int i = 0; i < 7; i++)
            {
                bool on = (mask & (1 << (6 - i))) != 0;
                dc.DrawGeometry(on ? Lit : Unlit, null, segs[i]);
            }

            Geometry Horizontal(double x1, double x2, double y) => Poly(
                new Point(x1, y), new Point(x1 + ht, y - ht), new Point(x2 - ht, y - ht),
                new Point(x2, y), new Point(x2 - ht, y + ht), new Point(x1 + ht, y + ht));

            Geometry Vertical(double cx, double y1, double y2) => Poly(
                new Point(cx, y1), new Point(cx + ht, y1 + ht), new Point(cx + ht, y2 - ht),
                new Point(cx, y2), new Point(cx - ht, y2 - ht), new Point(cx - ht, y1 + ht));
        }

        private static Geometry Poly(params Point[] pts)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], false, false);
            }
            g.Freeze();
            return g;
        }
    }
}
