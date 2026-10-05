using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GranRuedaDorada.Rendering;

namespace GranRuedaDorada.Controls
{
    public enum LightMode
    {
        /// <summary>Slow alternating blink, as a machine waiting for play.</summary>
        Idle,
        /// <summary>A fast running chase, for bonus anticipation.</summary>
        Chase,
        /// <summary>Every bulb flashing together, for wins.</summary>
        Flash,
    }

    /// <summary>Drives a set of incandescent-style bulbs through the cabinet light patterns.</summary>
    public sealed class BulbRing
    {
        // The ellipse is drawn 2.6x the bulb radius: the outer part of the gradient is the glow halo.
        private static readonly Brush On = Art.Radial(0.5, 0.5, 0.5,
            (0, "#FFFFFF"), (0.2, "#FFF4C0"), (0.36, "#F2B030"), (0.4, "#C0FFC040"), (0.6, "#50FFB020"), (1, "#00FFA000"));
        private static readonly Brush Off = Art.Radial(0.5, 0.5, 0.5,
            (0, "#A89060"), (0.3, "#6A4C18"), (0.38, "#402A08"), (0.4, "#00000000"), (1, "#00000000"));

        private readonly List<Ellipse> _bulbs = new();
        private readonly DispatcherTimer _timer = new();
        private LightMode _mode;
        private int _step;

        public BulbRing()
        {
            _timer.Tick += (_, _) => Step();
            Mode = LightMode.Idle;
            _timer.Start();
        }

        public Ellipse Add(Point center, double radius)
        {
            double r = radius * 2.6;
            var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = On, IsHitTestVisible = false };
            Canvas.SetLeft(e, center.X - r);
            Canvas.SetTop(e, center.Y - r);
            _bulbs.Add(e);
            return e;
        }

        public LightMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                _timer.Interval = TimeSpan.FromMilliseconds(value switch { LightMode.Chase => 45, LightMode.Flash => 110, _ => 520 });
            }
        }

        private void Step()
        {
            _step++;
            for (int i = 0; i < _bulbs.Count; i++)
            {
                bool lit = _mode switch
                {
                    LightMode.Chase => (i + _step) % 6 < 3,
                    LightMode.Flash => _step % 2 == 0,
                    _ => (i + _step) % 2 == 0,
                };
                _bulbs[i].Fill = lit ? On : Off;
            }
        }
    }

    /// <summary>A row of bulbs running around the edge of a rectangle (the marquee frame).</summary>
    public sealed class MarqueeLights : Canvas
    {
        public BulbRing Ring { get; } = new();

        public MarqueeLights(double width, double height, int count, double radius = 5)
        {
            Width = width;
            Height = height;
            IsHitTestVisible = false;
            double perimeter = 2 * (width + height), step = perimeter / count;
            for (int i = 0; i < count; i++)
            {
                double d = i * step, x, y;
                if (d < width) { x = d; y = 0; }
                else if ((d -= width) < height) { x = width; y = d; }
                else if ((d -= height) < width) { x = width - d; y = height; }
                else { d -= width; x = 0; y = height - d; }
                Children.Add(Ring.Add(new Point(x, y), radius));
            }
        }
    }
}
