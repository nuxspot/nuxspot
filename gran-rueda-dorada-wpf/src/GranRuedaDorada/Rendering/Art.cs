using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GranRuedaDorada.Engine;

namespace GranRuedaDorada.Rendering
{
    /// <summary>
    /// All game art is drawn as vectors at runtime, so it stays sharp at any window size.
    /// Symbols are 150x100 frozen DrawingImages shared by every reel cell.
    /// </summary>
    public static class Art
    {
        public const double SymW = 150, SymH = 100;

        public static readonly Typeface Display = new(new FontFamily("Impact, Arial Black, Segoe UI Black"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        public static readonly Typeface Heavy = new(new FontFamily("Arial Black, Segoe UI Black, Segoe UI"),
            FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
        public static readonly Typeface Narrow = new(new FontFamily("Bahnschrift SemiBold Condensed, Arial Narrow, Segoe UI"),
            FontStyles.Normal, FontWeights.Bold, FontStretches.Condensed);

        public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        public static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

        public static LinearGradientBrush Linear(Point start, Point end, params (double Offset, string Color)[] stops)
        {
            var b = new LinearGradientBrush { StartPoint = start, EndPoint = end };
            foreach (var (o, c) in stops) b.GradientStops.Add(new GradientStop(C(c), o));
            return Frozen(b);
        }

        public static LinearGradientBrush Vertical(params (double, string)[] stops) => Linear(new Point(0, 0), new Point(0, 1), stops);

        public static RadialGradientBrush Radial(double cx, double cy, double r, params (double Offset, string Color)[] stops)
        {
            var b = new RadialGradientBrush { GradientOrigin = new Point(cx, cy), Center = new Point(cx, cy), RadiusX = r, RadiusY = r };
            foreach (var (o, c) in stops) b.GradientStops.Add(new GradientStop(C(c), o));
            return Frozen(b);
        }

        public static SolidColorBrush Solid(string hex) => Frozen(new SolidColorBrush(C(hex)));

        // ---------- palette ----------
        public static readonly Brush Gold = Vertical((0, "#FFF6C8"), (0.45, "#F6C544"), (0.55, "#B47C10"), (1, "#F6C544"));
        public static readonly Brush GoldBright = Vertical((0, "#FFFDF0"), (0.4, "#FFE07A"), (0.6, "#E2A21C"), (1, "#FFE9A0"));
        public static readonly Brush Red = Vertical((0, "#FF8A8A"), (0.4, "#E01B2E"), (1, "#7A0614"));
        public static readonly Brush Blue = Vertical((0, "#9EC5FF"), (0.4, "#1F5FE0"), (1, "#0A2470"));
        public static readonly Brush BarFill = Vertical((0, "#4A4A58"), (0.5, "#121218"), (1, "#2A2A34"));
        public static readonly Brush CherryFill = Radial(0.35, 0.3, 0.75, (0, "#FFD0D6"), (0.25, "#FF2846"), (1, "#6A0012"));
        public static readonly Brush Leaf = Linear(new Point(0, 0), new Point(1, 1), (0, "#9CF06A"), (1, "#1E7A1C"));
        public static readonly Brush Coin = Radial(0.4, 0.35, 0.7, (0, "#FFFBE0"), (0.5, "#FFCF3A"), (1, "#9A6206"));
        public static readonly Brush Hub = Radial(0.4, 0.35, 0.7, (0, "#FFF8D0"), (0.55, "#F2B92C"), (1, "#7A4C04"));
        public static readonly Brush Shadow = Solid("#55000000");
        public static readonly Brush White = Brushes.White;

        public static readonly string[] WheelColors =
            { "#D81E3C", "#1D63D8", "#F2A614", "#2A9D48", "#8A2BD6", "#E8467F", "#13A7BF", "#F06B17", "#4C3BD1", "#C41F7A", "#2FB36B" };

        public static Pen Pen(Brush b, double w) => Frozen(new Pen(b, w) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });

        /// <summary>Text converted to an outline geometry centred on a point, so it can be filled and stroked like art.</summary>
        public static Geometry Text(string s, Typeface face, double size, Point center)
        {
            var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Brushes.Black, 1.0);
            var geo = ft.BuildGeometry(new Point(0, 0));
            var b = geo.Bounds;
            var g = new GeometryGroup();
            g.Children.Add(geo);
            if (!b.IsEmpty) g.Transform = new TranslateTransform(center.X - (b.Left + b.Width / 2), center.Y - (b.Top + b.Height / 2));
            return Frozen(g);
        }

        public static Point Polar(Point c, double r, double deg)
        {
            double a = deg * Math.PI / 180;
            return new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
        }

        public static Geometry Wedge(Point c, double r, double fromDeg, double toDeg, double innerR = 0)
        {
            var fig = new PathFigure { IsClosed = true, IsFilled = true };
            if (innerR <= 0)
            {
                fig.StartPoint = c;
                fig.Segments.Add(new LineSegment(Polar(c, r, fromDeg), true));
                fig.Segments.Add(new ArcSegment(Polar(c, r, toDeg), new Size(r, r), 0, toDeg - fromDeg > 180, SweepDirection.Clockwise, true));
            }
            else
            {
                fig.StartPoint = Polar(c, innerR, fromDeg);
                fig.Segments.Add(new LineSegment(Polar(c, r, fromDeg), true));
                fig.Segments.Add(new ArcSegment(Polar(c, r, toDeg), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
                fig.Segments.Add(new LineSegment(Polar(c, innerR, toDeg), true));
                fig.Segments.Add(new ArcSegment(Polar(c, innerR, fromDeg), new Size(innerR, innerR), 0, false, SweepDirection.Counterclockwise, true));
            }
            var g = new PathGeometry();
            g.Figures.Add(fig);
            return Frozen(g);
        }

        // ---------- symbols ----------
        private static readonly Dictionary<Sym, ImageSource> _sharp = new();
        private static readonly Dictionary<Sym, ImageSource> _blur = new();

        public static ImageSource Symbol(Sym s)
        {
            if (!_sharp.TryGetValue(s, out var img)) _sharp[s] = img = Image(dc => DrawSymbol(dc, s));
            return img;
        }

        /// <summary>A vertically smeared copy of a symbol, swapped in while a reel spins fast.</summary>
        public static ImageSource SymbolBlur(Sym s)
        {
            if (!_blur.TryGetValue(s, out var img))
            {
                _blur[s] = img = Image(dc =>
                {
                    const int copies = 7;
                    for (int i = 0; i < copies; i++)
                    {
                        double dy = (i - (copies - 1) / 2.0) * 7;
                        dc.PushTransform(new TranslateTransform(0, dy));
                        dc.PushOpacity(i == copies / 2 ? 0.45 : 0.2);
                        DrawSymbol(dc, s);
                        dc.Pop();
                        dc.Pop();
                    }
                });
            }
            return img;
        }

        private static ImageSource Image(Action<DrawingContext> draw)
        {
            var group = new DrawingGroup();
            using (var dc = group.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, SymW, SymH));
                draw(dc);
            }
            group.ClipGeometry = Frozen(new RectangleGeometry(new Rect(0, 0, SymW, SymH)));
            return Frozen(new DrawingImage(Frozen(group)));
        }

        private static void DrawSymbol(DrawingContext dc, Sym s)
        {
            switch (s)
            {
                case Sym.Seven: DrawSeven(dc); break;
                case Sym.Bar1: DrawBars(dc, 1); break;
                case Sym.Bar2: DrawBars(dc, 2); break;
                case Sym.Bar3: DrawBars(dc, 3); break;
                case Sym.Cherry: DrawCherry(dc); break;
                case Sym.Wild: DrawWild(dc); break;
                case Sym.Spin: DrawSpin(dc); break;
            }
        }

        private static readonly Geometry SevenShape = Frozen(Geometry.Parse("M 33,10 L 119,10 L 119,28 L 77,92 L 49,92 L 91,32 L 33,32 Z"));

        private static void DrawSeven(DrawingContext dc)
        {
            dc.PushTransform(new TranslateTransform(4, 5));
            dc.DrawGeometry(Shadow, Pen(Shadow, 9), SevenShape);
            dc.Pop();
            dc.DrawGeometry(null, Pen(Solid("#6B3A00"), 10), SevenShape);
            dc.DrawGeometry(Red, Pen(Gold, 7), SevenShape);
            dc.DrawGeometry(null, Pen(Solid("#8CFFFFFF"), 1.5), SevenShape);
            dc.DrawRoundedRectangle(Solid("#70FFFFFF"), null, new Rect(37, 14, 78, 6), 3, 3);
        }

        private static void DrawBars(DrawingContext dc, int n)
        {
            double h = n == 1 ? 34 : n == 2 ? 27 : 22, gap = 6, total = n * h + (n - 1) * gap;
            Brush edge = n == 1 ? Blue : n == 2 ? Red : Gold;
            for (int i = 0; i < n; i++)
            {
                double y = 50 - total / 2 + i * (h + gap);
                var r = new Rect(19, y, 112, h);
                dc.DrawRoundedRectangle(Shadow, null, new Rect(r.X + 3, r.Y + 4, r.Width, r.Height), 7, 7);
                dc.DrawRoundedRectangle(BarFill, Pen(edge, 4), r, 7, 7);
                dc.DrawRoundedRectangle(Solid("#30FFFFFF"), null, new Rect(r.X + 5, r.Y + 3, r.Width - 10, h * 0.32), 4, 4);
                dc.DrawGeometry(White, null, Text("BAR", Display, h * 0.82, new Point(75, y + h / 2)));
            }
        }

        private static void DrawCherry(DrawingContext dc)
        {
            var stems = Frozen(Geometry.Parse("M 57,64 C 61,38 73,20 85,10 M 97,68 C 93,44 89,24 85,10"));
            dc.DrawGeometry(null, Pen(Solid("#3B6D12"), 5), stems);
            var leaf = Frozen(Geometry.Parse("M 85,10 C 101,-4 121,4 125,16 C 109,24 93,20 85,10 Z"));
            dc.DrawGeometry(Leaf, Pen(Solid("#164F12"), 2), leaf);
            dc.DrawGeometry(null, Pen(Solid("#80164F12"), 1.2), Frozen(Geometry.Parse("M 87,11 C 100,10 112,13 123,16")));
            foreach (var (cx, cy, r) in new[] { (55.0, 72.0, 22.0), (97.0, 76.0, 21.0) })
            {
                dc.DrawEllipse(Shadow, null, new Point(cx + 3, cy + 4), r, r);
                dc.DrawEllipse(CherryFill, Pen(Solid("#4A0010"), 2), new Point(cx, cy), r, r);
                dc.PushTransform(new RotateTransform(-30, cx - 7, cy - 9));
                dc.DrawEllipse(Solid("#CCFFFFFF"), null, new Point(cx - 7, cy - 9), 6, 4);
                dc.Pop();
            }
        }

        private static void DrawWild(DrawingContext dc)
        {
            var c = new Point(75, 50);
            for (int i = 0; i < 16; i++)
            {
                dc.PushTransform(new RotateTransform(i * 22.5, c.X, c.Y));
                var ray = new StreamGeometry();
                using (var g = ray.Open())
                {
                    g.BeginFigure(c, true, true);
                    g.LineTo(new Point(c.X - 5, c.Y - 50), true, false);
                    g.LineTo(new Point(c.X + 5, c.Y - 50), true, false);
                }
                ray.Freeze();
                dc.DrawGeometry(Solid("#8CFFD34D"), null, ray);
                dc.Pop();
            }
            dc.DrawEllipse(Shadow, null, new Point(c.X + 3, c.Y + 4), 40, 40);
            dc.DrawEllipse(Coin, Pen(Solid("#8A5300"), 3), c, 40, 40);
            dc.DrawEllipse(null, Frozen(new Pen(Solid("#FFF7C0"), 2) { DashStyle = Frozen(new DashStyle(new double[] { 2, 2 }, 0)) }), c, 33, 33);
            var x2 = Text("2X", Heavy, 30, new Point(75, 42));
            dc.DrawGeometry(null, Pen(White, 6), x2);
            dc.DrawGeometry(Solid("#C8102E"), null, x2);
            var band = new Rect(39, 63, 72, 19);
            dc.DrawRoundedRectangle(Blue, Pen(Solid("#FFF3B0"), 2), band, 4, 4);
            dc.DrawGeometry(White, null, Text("COMODÍN", Narrow, 14, new Point(75, 72.5)));
        }

        private static void DrawSpin(DrawingContext dc)
        {
            var c = new Point(75, 44);
            string[] colors = { "#E5243B", "#F6A313", "#2F9E44", "#1C6BD6", "#8E2BD9", "#FF5FA2", "#16B5C9", "#FFD21F" };
            dc.DrawEllipse(Shadow, null, new Point(c.X + 3, c.Y + 4), 40, 40);
            for (int i = 0; i < 8; i++)
                dc.DrawGeometry(Solid(colors[i]), null, Wedge(c, 38, i * 45 - 22.5, i * 45 + 22.5));
            dc.DrawEllipse(null, Pen(Gold, 6), c, 38, 38);
            dc.DrawEllipse(Hub, null, c, 9, 9);
            var band = new Rect(27, 64, 96, 30);
            dc.DrawRoundedRectangle(Red, Pen(Gold, 3), band, 8, 8);
            var t = Text("GIRA", Heavy, 21, new Point(75, 79));
            dc.DrawGeometry(null, Pen(Solid("#5A0414"), 4), t);
            dc.DrawGeometry(White, null, t);
        }

        // ---------- coin for celebrations ----------
        private static ImageSource? _coin;

        public static ImageSource CoinImage
        {
            get
            {
                if (_coin != null) return _coin;
                var g = new DrawingGroup();
                using (var dc = g.Open())
                {
                    var c = new Point(24, 24);
                    dc.DrawEllipse(Solid("#8A5300"), null, c, 24, 24);
                    dc.DrawEllipse(Coin, null, c, 21, 21);
                    dc.DrawEllipse(null, Pen(Solid("#FFF7C0"), 1.5), c, 16, 16);
                    var star = Text("★", new Typeface("Segoe UI Symbol"), 22, c);
                    dc.DrawGeometry(Solid("#C88A10"), null, star);
                }
                return _coin = Frozen(new DrawingImage(Frozen(g)));
            }
        }
    }
}
