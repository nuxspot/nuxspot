using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using GranRuedaDorada.Engine;
using GranRuedaDorada.Rendering;

namespace GranRuedaDorada.Controls
{
    /// <summary>
    /// One mechanical reel seen through curved glass: three visible stops, the middle one on the payline.
    /// The strip is rendered three times end to end so it can scroll forever without a seam.
    /// </summary>
    public sealed class ReelView : Grid
    {
        public const double CellHeight = 111;

        private readonly Sym[] _strip;
        private readonly int _len;
        private readonly Canvas _canvas = new() { ClipToBounds = true };
        private readonly StackPanel _stack = new();
        private readonly TranslateTransform _scroll = new();
        private readonly Image[] _images;
        private double _pos; // index of the top visible stop; fractional while spinning
        private bool _blurred;

        public ReelView(Sym[] strip, int startStop)
        {
            _strip = strip;
            _len = strip.Length;
            _images = new Image[_len * 3];

            Background = Art.Vertical((0, "#E9E3D2"), (0.5, "#FFFDF6"), (1, "#E9E3D2"));
            ClipToBounds = true;

            _stack.RenderTransform = _scroll;
            for (int i = 0; i < _images.Length; i++)
            {
                var img = new Image
                {
                    Source = Art.Symbol(strip[i % _len]),
                    Width = Art.SymW,
                    Height = Art.SymH,
                    Stretch = Stretch.Uniform,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1, 1),
                };
                _images[i] = img;
                _stack.Children.Add(new Border { Height = CellHeight, Child = img });
            }
            _canvas.Children.Add(_stack);
            Children.Add(_canvas);

            // Curved-glass shading: dark at the top and bottom edges, a soft highlight just above centre.
            Children.Add(new Rectangle
            {
                IsHitTestVisible = false,
                Fill = Art.Vertical((0, "#A0000000"), (0.2, "#18000000"), (0.42, "#28FFFFFF"), (0.55, "#00000000"),
                                    (0.8, "#18000000"), (1, "#A8000000")),
            });

            SizeChanged += (_, _) => _stack.Width = ActualWidth;
            _pos = Mod(startStop - 1, _len);
            Place();
        }

        private static double Mod(double a, double n) => ((a % n) + n) % n;

        private void Place() => _scroll.Y = -Mod(_pos, _len) * CellHeight;

        private void SetBlur(bool on)
        {
            if (on == _blurred) return;
            _blurred = on;
            for (int i = 0; i < _images.Length; i++)
                _images[i].Source = on ? Art.SymbolBlur(_strip[i % _len]) : Art.Symbol(_strip[i % _len]);
        }

        /// <summary>
        /// Spins and lands with <paramref name="targetStop"/> on the payline.
        /// Motion: a short kick back (the reel "winds up"), full speed, a slowing glide, then a small mechanical bounce.
        /// </summary>
        public Task SpinAsync(int targetStop, double durationMs, int laps, Action? landed = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            double from = _pos;
            double dist = laps * _len + Mod(Mod(targetStop - 1, _len) - Mod(from, _len), _len);
            const double windupMs = 110, bounceMs = 220, overshoot = 0.22;
            double mainMs = Math.Max(200, durationMs - windupMs - bounceMs);
            var clock = Stopwatch.StartNew();
            double last = from;
            bool landedFired = false;

            EventHandler? tick = null;
            tick = (_, _) =>
            {
                double ms = clock.Elapsed.TotalMilliseconds;
                double offset;
                if (ms < windupMs)
                {
                    offset = -0.28 * Math.Sin(Math.PI * ms / windupMs);
                }
                else if (ms < windupMs + mainMs)
                {
                    double u = (ms - windupMs) / mainMs;
                    offset = (dist + overshoot) * (1 - Math.Pow(1 - u, 3));
                }
                else
                {
                    if (!landedFired) { landedFired = true; landed?.Invoke(); }
                    double v = Math.Min(1, (ms - windupMs - mainMs) / bounceMs);
                    offset = dist + overshoot * (1 - v) * Math.Cos(v * Math.PI * 1.5);
                }

                _pos = from + offset;
                SetBlur(Math.Abs(_pos - last) > 0.32);
                last = _pos;
                Place();

                if (ms >= durationMs)
                {
                    CompositionTarget.Rendering -= tick;
                    _pos = Mod(from + dist, _len);
                    SetBlur(false);
                    Place();
                    tcs.TrySetResult(true);
                }
            };
            CompositionTarget.Rendering += tick;
            return tcs.Task;
        }

        /// <summary>Pulses the symbol in a visible row (0 = top, 1 = payline, 2 = bottom).</summary>
        public void Highlight(int row)
        {
            var img = _images[(int)Math.Round(Mod(_pos, _len)) + row];
            var pulse = new DoubleAnimation(1, 1.13, TimeSpan.FromMilliseconds(420))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            var scale = (ScaleTransform)img.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
            img.Effect = new DropShadowEffect { Color = Art.C("#FFD34D"), BlurRadius = 22, ShadowDepth = 0, Opacity = 1 };
        }

        public void ClearHighlights()
        {
            foreach (var img in _images)
            {
                if (img.Effect == null) continue;
                var scale = (ScaleTransform)img.RenderTransform;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                img.Effect = null;
            }
        }
    }
}
