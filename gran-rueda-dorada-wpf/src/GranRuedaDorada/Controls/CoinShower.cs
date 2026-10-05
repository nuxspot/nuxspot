using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GranRuedaDorada.Rendering;

namespace GranRuedaDorada.Controls
{
    /// <summary>Gold coins fountaining up from the coin tray and tumbling down, for big wins.</summary>
    public sealed class CoinShower : Canvas
    {
        private sealed class Coin
        {
            public required Image Image;
            public required ScaleTransform Flip;
            public double X, Y, Vx, Vy, Spin, Phase;
        }

        private readonly List<Coin> _coins = new();
        private readonly Random _rng = new();
        private readonly Stopwatch _clock = new();
        private double _lastMs, _emitUntilMs, _emitRate, _emitDebt;
        private bool _running;

        public CoinShower()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;
        }

        /// <summary>Emits coins for <paramref name="seconds"/> at <paramref name="perSecond"/>.</summary>
        public void Burst(double seconds, double perSecond)
        {
            if (!_running)
            {
                _running = true;
                _clock.Restart();
                _lastMs = 0;
                CompositionTarget.Rendering += OnFrame;
            }
            _emitUntilMs = _clock.Elapsed.TotalMilliseconds + seconds * 1000;
            _emitRate = perSecond;
        }

        public void StopEmitting() => _emitUntilMs = 0;

        private void Emit()
        {
            double size = 30 + _rng.NextDouble() * 22;
            var flip = new ScaleTransform(1, 1);
            var img = new Image { Source = Art.CoinImage, Width = size, Height = size, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = flip };
            var c = new Coin
            {
                Image = img,
                Flip = flip,
                X = ActualWidth / 2 + (_rng.NextDouble() - 0.5) * 220,
                Y = ActualHeight + 20,
                Vx = (_rng.NextDouble() - 0.5) * 700,
                Vy = -(1300 + _rng.NextDouble() * 700),
                Spin = 6 + _rng.NextDouble() * 10,
                Phase = _rng.NextDouble() * Math.PI * 2,
            };
            _coins.Add(c);
            Children.Add(img);
        }

        private void OnFrame(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            double dt = Math.Min(0.05, (now - _lastMs) / 1000);
            _lastMs = now;

            if (now < _emitUntilMs)
            {
                _emitDebt += _emitRate * dt;
                while (_emitDebt >= 1) { Emit(); _emitDebt -= 1; }
            }

            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                c.Vy += 2000 * dt;
                c.X += c.Vx * dt;
                c.Y += c.Vy * dt;
                c.Phase += c.Spin * dt;
                c.Flip.ScaleX = Math.Cos(c.Phase);
                SetLeft(c.Image, c.X - c.Image.Width / 2);
                SetTop(c.Image, c.Y - c.Image.Height / 2);
                if (c.Vy > 0 && c.Y > ActualHeight + 60)
                {
                    Children.Remove(c.Image);
                    _coins.RemoveAt(i);
                }
            }

            if (_coins.Count == 0 && now >= _emitUntilMs)
            {
                CompositionTarget.Rendering -= OnFrame;
                _running = false;
            }
        }
    }
}
