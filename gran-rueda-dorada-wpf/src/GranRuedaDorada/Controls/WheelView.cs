using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using GranRuedaDorada.Rendering;

namespace GranRuedaDorada.Controls
{
    /// <summary>
    /// The top-box bonus wheel: a 600x600 lit wheel with pegs between segments and a flapper pointer at 12 o'clock.
    /// </summary>
    public sealed class WheelView : Canvas
    {
        private const double Size = 600, R = 262;
        private static readonly Point Center = new(300, 300);

        private readonly int[] _values;
        private readonly double _seg;
        private readonly RotateTransform _rotation = new(0, 300, 300);
        private readonly RotateTransform _pointerRotation = new(0, 300, 10);
        private readonly Path _winWedge;
        private readonly RotateTransform _winWedgeRotation = new(0, 300, 300);
        private readonly Ellipse _glow;
        private double _angle;

        public BulbRing Lights { get; } = new();

        /// <summary>Raised each time a peg passes the pointer.</summary>
        public event Action? PegPassed;

        public WheelView(int[] values)
        {
            _values = values;
            _seg = 360.0 / values.Length;
            Width = Height = Size;

            // Back-light behind the wheel; brightens during the bonus.
            _glow = new Ellipse
            {
                Width = 700, Height = 700,
                Fill = Art.Radial(0.5, 0.5, 0.5, (0, "#90FFD060"), (0.6, "#40FF9020"), (1, "#00FF8000")),
                Opacity = 0.35,
                IsHitTestVisible = false,
            };
            SetLeft(_glow, -50); SetTop(_glow, -50);
            Children.Add(_glow);

            Children.Add(Disc(296, Art.Solid("#70000000"), 6));
            Children.Add(Disc(294, Art.Gold));
            Children.Add(Disc(286, Art.Vertical((0, "#B47C10"), (0.5, "#FFE9A0"), (1, "#7A4C04"))));
            Children.Add(Disc(270, Art.Solid("#2A1A04")));

            var face = new Canvas { Width = Size, Height = Size, RenderTransform = _rotation };
            face.Children.Add(new Image { Source = BuildFace(), Width = Size, Height = Size });
            _winWedge = new Path
            {
                Data = Art.Wedge(Center, R, -_seg / 2, _seg / 2),
                Fill = Brushes.White,
                Opacity = 0,
                RenderTransform = _winWedgeRotation,
                IsHitTestVisible = false,
            };
            face.Children.Add(_winWedge);
            Children.Add(face);

            var bulbs = new Canvas { Width = Size, Height = Size, IsHitTestVisible = false };
            for (int i = 0; i < 44; i++)
                bulbs.Children.Add(Lights.Add(Art.Polar(Center, 281, i * (360.0 / 44) + 4), 6));
            Children.Add(bulbs);

            Children.Add(new Image { Source = BuildHub(), Width = 160, Height = 160 });
            SetLeft(Children[Children.Count - 1], 220); SetTop(Children[Children.Count - 1], 220);

            var pointer = new Path
            {
                Data = Art.Frozen(Geometry.Parse("M 280,-4 L 320,-4 L 320,20 L 300,62 L 280,20 Z")),
                Fill = Art.Red,
                Stroke = Art.Gold,
                StrokeThickness = 4,
                StrokeLineJoin = PenLineJoin.Round,
                RenderTransform = _pointerRotation,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 4, Opacity = 0.6 },
            };
            Children.Add(pointer);
            var pivot = new Ellipse { Width = 14, Height = 14, Fill = Art.Hub };
            SetLeft(pivot, 293); SetTop(pivot, 3);
            Children.Add(pivot);
        }

        private static Ellipse Disc(double r, Brush fill, double dy = 0)
        {
            var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill, IsHitTestVisible = false };
            SetLeft(e, 300 - r);
            SetTop(e, 300 - r + dy);
            return e;
        }

        private ImageSource BuildFace()
        {
            var g = new DrawingGroup();
            using (var dc = g.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Size, Size));
                var divider = Art.Pen(Art.Solid("#5A3A06"), 1.5);
                for (int i = 0; i < _values.Length; i++)
                {
                    int v = _values[i];
                    Brush fill = v >= 1000 ? Art.Vertical((0, "#262626"), (1, "#050505"))
                               : v >= 500 ? Art.Vertical((0, "#FFFFFF"), (1, "#E6DFCC"))
                               : Art.Solid(Art.WheelColors[i % Art.WheelColors.Length]);
                    dc.DrawGeometry(fill, divider, Art.Wedge(Center, R, i * _seg - _seg / 2, i * _seg + _seg / 2));
                }

                // Shading: light falls off towards the rim, like a lit, slightly dished wheel.
                dc.DrawEllipse(Art.Radial(0.5, 0.5, 0.5, (0, "#30FFFFFF"), (0.55, "#10FFFFFF"), (0.85, "#00000000"), (1, "#50000000")),
                    null, Center, R, R);

                for (int i = 0; i < _values.Length; i++)
                {
                    int v = _values[i];
                    string digits = v.ToString();
                    double size = digits.Length == 4 ? 31 : 35;
                    Brush txt = v >= 1000 ? Art.GoldBright : v >= 500 ? Art.Solid("#C8102E") : Brushes.White;
                    Pen outline = Art.Pen(Art.Solid(v >= 500 && v < 1000 ? "#80FFFFFF" : "#90000000"), 2.2);
                    dc.PushTransform(new RotateTransform(i * _seg, Center.X, Center.Y));
                    for (int k = 0; k < digits.Length; k++)
                    {
                        var geo = Art.Text(digits[k].ToString(), Art.Display, size, new Point(Center.X, Center.Y - R + 36 + k * size * 0.95));
                        dc.DrawGeometry(null, outline, geo);
                        dc.DrawGeometry(txt, null, geo);
                    }
                    dc.Pop();

                    var peg = Art.Polar(Center, R - 9, i * _seg - _seg / 2);
                    dc.DrawEllipse(Art.Solid("#60000000"), null, new Point(peg.X + 1.5, peg.Y + 2), 5.5, 5.5);
                    dc.DrawEllipse(Art.Hub, Art.Pen(Art.Solid("#5A3A06"), 1), peg, 5.5, 5.5);
                }
            }
            return Art.Frozen(new DrawingImage(Art.Frozen(g)));
        }

        private static ImageSource BuildHub()
        {
            var g = new DrawingGroup();
            using (var dc = g.Open())
            {
                var c = new Point(80, 80);
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 160, 160)); // fixes the image bounds
                dc.DrawEllipse(Art.Solid("#60000000"), null, new Point(82, 86), 62, 62);
                dc.DrawEllipse(Art.Hub, Art.Pen(Art.Solid("#6B4300"), 4), c, 60, 60);
                dc.DrawEllipse(null, Art.Frozen(new Pen(Art.Solid("#FFF6C8"), 2) { DashStyle = Art.Frozen(new DashStyle(new double[] { 1.5, 2.5 }, 0)) }), c, 48, 48);
                var star = Art.Frozen(Geometry.Parse("M 80,40 L 90,68 L 119,68 L 96,85 L 105,113 L 80,96 L 55,113 L 64,85 L 41,68 L 70,68 Z"));
                dc.DrawGeometry(Art.Red, Art.Pen(Art.Solid("#FFF6C8"), 2.5), star);
                dc.DrawEllipse(Art.Solid("#50FFFFFF"), null, new Point(64, 58), 18, 9);
            }
            return Art.Frozen(new DrawingImage(Art.Frozen(g)));
        }

        public void SetExcited(bool excited)
        {
            Lights.Mode = excited ? LightMode.Chase : LightMode.Idle;
            _glow.BeginAnimation(OpacityProperty, excited
                ? new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
                : new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(300)));
        }

        public void ClearWin()
        {
            _winWedge.BeginAnimation(OpacityProperty, null);
            _winWedge.Opacity = 0;
        }

        /// <summary>Spins at least five turns and stops with segment <paramref name="index"/> under the pointer.</summary>
        public Task SpinToAsync(int index, Random jitterSource)
        {
            var tcs = new TaskCompletionSource<bool>();
            double jitter = (jitterSource.NextDouble() - 0.5) * _seg * 0.7;
            double current = ((_angle % 360) + 360) % 360;
            double want = ((-(index * _seg) - jitter) % 360 + 360) % 360;
            double delta = 5 * 360 + ((want - current) % 360 + 360) % 360;
            double from = _angle;
            const double durationMs = 8200;
            int lastPeg = (int)Math.Floor((from + _seg / 2) / _seg);
            var clock = Stopwatch.StartNew();

            EventHandler? tick = null;
            tick = (_, _) =>
            {
                double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / durationMs);
                // Quick push to full speed, then a long friction-like slowdown.
                double ramp = Math.Min(1, t / 0.04);
                double e = 1 - Math.Pow(1 - t, 3.4);
                _angle = from + delta * (t < 0.04 ? e * ramp : e);
                _rotation.Angle = _angle;

                int peg = (int)Math.Floor((_angle + _seg / 2) / _seg);
                if (peg != lastPeg)
                {
                    lastPeg = peg;
                    PegPassed?.Invoke();
                    _pointerRotation.BeginAnimation(RotateTransform.AngleProperty,
                        new DoubleAnimation(-26, 0, TimeSpan.FromMilliseconds(140)) { EasingFunction = new BackEase { Amplitude = 0.6 } });
                }

                if (t >= 1)
                {
                    CompositionTarget.Rendering -= tick;
                    _winWedgeRotation.Angle = index * _seg;
                    _winWedge.BeginAnimation(OpacityProperty,
                        new DoubleAnimation(0, 0.5, TimeSpan.FromMilliseconds(150)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                    tcs.TrySetResult(true);
                }
            };
            CompositionTarget.Rendering += tick;
            return tcs.Task;
        }
    }
}
