using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using GranRuedaDorada.Audio;
using GranRuedaDorada.Controls;
using GranRuedaDorada.Engine;
using GranRuedaDorada.Rendering;
using WpfPath = System.Windows.Shapes.Path;

namespace GranRuedaDorada
{
    public partial class MainWindow : Window
    {
        private enum Phase { Idle, Spinning, Rollup, BonusReady, WheelSpinning }

        private const long RefillCredits = 1000;

        private readonly SlotEngine _engine = new();
        private readonly SoundBank _sound = new();
        private readonly SavedState _state = SavedState.Load();
        private readonly Random _jitter = new();
        private readonly ReelView[] _reels = new ReelView[3];
        private WheelView _wheel = null!;
        private MarqueeLights _marquee = null!;

        private Phase _phase = Phase.Idle;
        private long _credits;
        private int _bet;
        private long _lastWin;
        private bool _skipRollup;

        public MainWindow()
        {
            InitializeComponent();
            _credits = _state.Credits;
            _bet = _state.Bet;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            BuildTitle();
            _marquee = new MarqueeLights(660, 92, 56);
            MarqueeLightsHost.Children.Add(_marquee);

            _wheel = new WheelView(SlotEngine.WheelValues);
            _wheel.PegPassed += () => _sound.Play(Sfx.WheelTick);
            WheelHost.Children.Add(_wheel);

            var rng = new Random();
            for (int r = 0; r < 3; r++)
            {
                _reels[r] = new ReelView(SlotEngine.Strips[r], rng.Next(SlotEngine.StopsPerReel)) { Margin = new Thickness(4, 0, 4, 0) };
                Grid.SetColumn(_reels[r], r);
                ReelGrid.Children.Add(_reels[r]);
            }

            BuildPaytable();
            UpdateUi();

            try
            {
                await _sound.InitializeAsync();
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                // No writable temp folder: play without sound.
            }
        }

        // ================= art built in code =================

        private void BuildTitle()
        {
            var geo = Art.Text("GRAN RUEDA DORADA", Art.Display, 66, new Point(300, 40));
            TitleHost.Children.Add(new WpfPath { Data = geo, Stroke = Art.Solid("#3A0410"), StrokeThickness = 9, StrokeLineJoin = PenLineJoin.Round });
            TitleHost.Children.Add(new WpfPath
            {
                Data = geo,
                Fill = Art.GoldBright,
                Stroke = Art.Solid("#FFF6C8"),
                StrokeThickness = 1,
                Effect = new DropShadowEffect { Color = Art.C("#FFC850"), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.8 },
            });
        }

        private void SetBigWinTitle(string text)
        {
            BigWinTitleHost.Children.Clear();
            var geo = Art.Text(text, Art.Display, 84, new Point(320, 60));
            BigWinTitleHost.Children.Add(new WpfPath { Data = geo, Stroke = Art.Solid("#6B1A00"), StrokeThickness = 14, StrokeLineJoin = PenLineJoin.Round });
            BigWinTitleHost.Children.Add(new WpfPath
            {
                Data = geo,
                Fill = Art.GoldBright,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                Effect = new DropShadowEffect { Color = Art.C("#FFB020"), BlurRadius = 30, ShadowDepth = 0 },
            });
        }

        private void BuildPaytable()
        {
            PayRows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            PayRows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 3; i++) PayRows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });

            (Sym[] Art, string Name, string[] Pays)[] rows = new (Sym[] Art, string Name, string[] Pays)[]
            {
                (new[] { Sym.Wild, Sym.Wild, Sym.Wild }, "3 Comodines", SlotEngine.TopAward.Select(p => p.ToString()).ToArray()),
            }.Concat(SlotEngine.Combos.Select(c => (c.Art, c.Name, new[] { 1, 2, 3 }.Select(b => (c.Pay * b).ToString()).ToArray())))
             .Concat(new[]
             {
                 (new[] { Sym.Cherry, Sym.Cherry }, "2 Cerezas", new[] { 1, 2, 3 }.Select(b => (SlotEngine.TwoCherries * b).ToString()).ToArray()),
                 (new[] { Sym.Cherry }, "1 Cereza", new[] { 1, 2, 3 }.Select(b => (SlotEngine.OneCherry * b).ToString()).ToArray()),
                 (new[] { Sym.Spin }, "Rueda (3 créditos)", new[] { "—", "—", "25–1000" }),
             }).ToArray();

            AddPayRow(0, null, "COMBINACIÓN", new[] { "1 CRÉD.", "2 CRÉD.", "3 CRÉD." }, header: true);
            for (int i = 0; i < rows.Length; i++) AddPayRow(i + 1, rows[i].Art, rows[i].Name, rows[i].Pays, header: false);

            var (rtp, hit, bonus) = SlotEngine.Analyze(SlotEngine.MaxBet);
            PayNotes.Text =
                "COMODÍN 2X sustituye a todos los símbolos excepto GIRA. Un comodín en una combinación ganadora duplica el premio; dos lo multiplican por 4.\n" +
                $"Rueda: 22 casillas de 25 a 1000 créditos. Con apuesta máxima: retorno teórico {rtp:P1}, premio en {hit:P0} de los giros, rueda 1 de cada {1 / bonus:F0} giros.\n" +
                "Juego de demostración con créditos ficticios. No usa dinero real.";
        }

        private void AddPayRow(int row, Sym[]? art, string name, string[] pays, bool header)
        {
            PayRows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(header ? 30 : 50) });
            if (!header)
            {
                var line = new Border { BorderBrush = Art.Solid("#30F6C544"), BorderThickness = new Thickness(0, 1, 0, 0) };
                Grid.SetRow(line, row);
                Grid.SetColumnSpan(line, 5);
                PayRows.Children.Add(line);
            }
            if (art != null)
            {
                var icons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                foreach (var s in art) icons.Children.Add(new Image { Source = Art.Symbol(s), Width = 63, Height = 42 });
                Grid.SetRow(icons, row);
                PayRows.Children.Add(icons);
            }
            var label = new TextBlock
            {
                Text = name,
                Foreground = header ? Art.Solid("#F6C544") : Brushes.White,
                FontFamily = new FontFamily(header ? "Arial Black" : "Segoe UI Semibold"),
                FontSize = header ? 12 : 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(header ? 0 : 6, 0, 0, 0),
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, header ? 0 : 1);
            if (header) Grid.SetColumnSpan(label, 2);
            PayRows.Children.Add(label);

            for (int i = 0; i < 3; i++)
            {
                var v = new TextBlock
                {
                    Text = pays[i],
                    Foreground = header ? Art.Solid("#F6C544") : i == 2 ? Art.Solid("#FFD34D") : Art.Solid("#FF4A3A"),
                    FontFamily = new FontFamily(header ? "Arial Black" : "Consolas"),
                    FontWeight = FontWeights.Bold,
                    FontSize = header ? 12 : 22,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetRow(v, row);
                Grid.SetColumn(v, 2 + i);
                PayRows.Children.Add(v);
            }
        }

        // ================= UI state =================

        private void UpdateUi()
        {
            CreditMeter.Value = _credits;
            BetMeter.Value = _bet;
            WinMeter.Value = _lastWin;

            bool idle = _phase == Phase.Idle;
            BetButton.IsEnabled = idle;
            MaxButton.IsEnabled = idle && _credits > 0;
            InfoButton.IsEnabled = idle || _phase == Phase.BonusReady;
            SpinButton.IsEnabled = _phase is Phase.Idle or Phase.Rollup or Phase.BonusReady;

            if (_phase == Phase.BonusReady) { SpinLabel.Text = "RUEDA"; SpinLabel.FontSize = 34; }
            else if (idle && _credits <= 0) { SpinLabel.Text = "RECARGAR"; SpinLabel.FontSize = 26; }
            else { SpinLabel.Text = "GIRAR"; SpinLabel.FontSize = 34; }
        }

        private void SaveState()
        {
            _state.Credits = _credits;
            _state.Bet = _bet;
            _state.Save();
        }

        private void ClearWin()
        {
            foreach (var r in _reels) r.ClearHighlights();
            WinPlaque.Visibility = Visibility.Collapsed;
            Payline.BeginAnimation(OpacityProperty, null);
            Payline.Opacity = 0.75;
            _wheel.ClearWin();
            BigWinOverlay.Visibility = Visibility.Collapsed;
        }

        // ================= game flow =================

        private async void OnSpin(object sender, RoutedEventArgs e) => await SpinPressedAsync();

        private async Task SpinPressedAsync()
        {
            switch (_phase)
            {
                case Phase.Rollup:
                    _skipRollup = true;
                    return;
                case Phase.BonusReady:
                    await SpinWheelAsync();
                    return;
                case Phase.Idle:
                    break;
                default:
                    return;
            }

            if (_credits <= 0)
            {
                _credits = RefillCredits;
                _lastWin = 0;
                SaveState();
                _sound.Play(Sfx.Credit);
                UpdateUi();
                return;
            }
            if (_bet > _credits) _bet = (int)_credits;

            ClearWin();
            _credits -= _bet;
            _lastWin = 0;
            SaveState();
            _phase = Phase.Spinning;
            UpdateUi();

            var result = _engine.Spin(_bet);
            // Hold the last reel when it can deliver the bonus or the top award.
            bool tease = result.Bonus || (_bet == SlotEngine.MaxBet && result.Line[0] == Sym.Wild && result.Line[1] == Sym.Wild);

            _sound.Play(Sfx.ReelStart);
            _sound.Loop(Sfx.ReelLoop);
            void Landed() => _sound.Play(Sfx.ReelStop);
            var t0 = _reels[0].SpinAsync(result.Stops[0], 1050, 4, Landed);
            var t1 = _reels[1].SpinAsync(result.Stops[1], 1450, 5, Landed);
            var t2 = _reels[2].SpinAsync(result.Stops[2], tease ? 3500 : 1850, tease ? 10 : 6, Landed);

            if (tease)
            {
                await t1;
                StartAnticipation();
            }
            await Task.WhenAll(t0, t1, t2);
            _sound.Stop(Sfx.ReelLoop);
            StopAnticipation();

            if (result.Win.Pay > 0)
            {
                foreach (int r in result.Win.Reels) _reels[r].Highlight(1);
                Payline.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.15, TimeSpan.FromMilliseconds(160))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                });
                await CelebrateAsync(result.Win.Pay, result.Win.Name, fromWheel: false);
            }

            if (result.Bonus)
            {
                EnterBonus();
                return;
            }

            _phase = Phase.Idle;
            UpdateUi();
        }

        private void StartAnticipation()
        {
            _sound.Play(Sfx.Anticipation);
            Reel3Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(180))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
            _wheel.Lights.Mode = LightMode.Chase;
            _marquee.Ring.Mode = LightMode.Chase;
        }

        private void StopAnticipation()
        {
            _sound.Stop(Sfx.Anticipation);
            Reel3Glow.BeginAnimation(OpacityProperty, null);
            Reel3Glow.Opacity = 0;
            _wheel.Lights.Mode = LightMode.Idle;
            _marquee.Ring.Mode = LightMode.Idle;
        }

        private void EnterBonus()
        {
            _phase = Phase.BonusReady;
            _reels[2].Highlight(1);
            _sound.Play(Sfx.BonusTrigger);
            _sound.Play(Sfx.Applause);
            _wheel.SetExcited(true);
            _marquee.Ring.Mode = LightMode.Chase;
            FlashCabinet(8);

            BonusBanner.Visibility = Visibility.Visible;
            var pulse = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(380))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase(),
            };
            BonusBannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            BonusBannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);

            var glow = new DropShadowEffect { Color = Art.C("#7DFF9A"), BlurRadius = 40, ShadowDepth = 0 };
            glow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(420))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
            SpinButton.Effect = glow;
            UpdateUi();
        }

        private async Task SpinWheelAsync()
        {
            if (_phase != Phase.BonusReady) return;
            _phase = Phase.WheelSpinning;
            UpdateUi();
            BonusBanner.Visibility = Visibility.Collapsed;
            BonusBannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BonusBannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            SpinButton.Effect = null;
            _sound.Play(Sfx.WheelStart);

            int index = _engine.PickWheelSegment();
            await _wheel.SpinToAsync(index, _jitter);
            int value = SlotEngine.WheelValues[index];

            _wheel.Lights.Mode = LightMode.Flash;
            await CelebrateAsync(value, "PREMIO DE LA RUEDA", fromWheel: true);
            _wheel.SetExcited(false);
            _marquee.Ring.Mode = LightMode.Idle;
            _phase = Phase.Idle;
            UpdateUi();
        }

        private async void OnWheelClicked(object sender, MouseButtonEventArgs e)
        {
            if (_phase == Phase.BonusReady) await SpinWheelAsync();
        }

        /// <summary>Win presentation scaled to the size of the win: bells, lights, and for big wins a fanfare, applause and coins.</summary>
        private async Task CelebrateAsync(int amount, string name, bool fromWheel)
        {
            int bet = Math.Max(1, _bet);
            bool big = amount >= 40 * bet || (fromWheel && amount >= 200);
            bool medium = !big && (amount >= 10 * bet || fromWheel);

            WinNameText.Text = name.ToUpperInvariant();
            WinAmountText.Text = "0";
            WinPlaque.Visibility = Visibility.Visible;
            var pop = new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = new BackEase { Amplitude = 0.7 } };
            WinPlaqueScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            WinPlaqueScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

            double seconds;
            if (big)
            {
                _sound.Play(Sfx.BigWin);
                _sound.Play(Sfx.Applause);
                _marquee.Ring.Mode = LightMode.Flash;
                _wheel.Lights.Mode = LightMode.Flash;
                FlashCabinet(14);
                SetBigWinTitle(amount >= 100 * bet || amount >= 500 ? "¡SÚPER PREMIO!" : "¡GRAN PREMIO!");
                BigWinAmount.Text = "0";
                BigWinOverlay.Visibility = Visibility.Visible;
                var bigPop = new DoubleAnimation(0.1, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 5 } };
                BigWinScale.BeginAnimation(ScaleTransform.ScaleXProperty, bigPop);
                BigWinScale.BeginAnimation(ScaleTransform.ScaleYProperty, bigPop);
                Coins.Burst(Math.Min(7, 2.5 + amount / 150.0), 45);
                seconds = Math.Min(7, 2.5 + amount / 120.0);
            }
            else if (medium)
            {
                _sound.Play(Sfx.SmallWin);
                _marquee.Ring.Mode = LightMode.Flash;
                FlashCabinet(6);
                Coins.Burst(1.2, 18);
                seconds = Math.Min(3.5, 1.2 + amount / 60.0);
            }
            else
            {
                _sound.Play(Sfx.SmallWin);
                seconds = Math.Min(1.6, 0.5 + amount / 30.0);
            }

            await RollupAsync(amount, seconds, shown =>
            {
                WinAmountText.Text = shown.ToString();
                if (big) BigWinAmount.Text = shown.ToString();
            });

            if (big)
            {
                await Task.Delay(1400);
                Coins.StopEmitting();
                BigWinOverlay.Visibility = Visibility.Collapsed;
            }
            _marquee.Ring.Mode = LightMode.Idle;
            if (!fromWheel) _wheel.Lights.Mode = LightMode.Idle;
        }

        private async Task RollupAsync(int amount, double seconds, Action<long> shown)
        {
            var previous = _phase;
            _phase = Phase.Rollup;
            _skipRollup = false;
            UpdateUi();

            long startCredits = _credits, startWin = _lastWin;
            _sound.Loop(Sfx.RollupLoop);
            var clock = Stopwatch.StartNew();
            while (true)
            {
                double t = _skipRollup ? 1 : Math.Min(1, clock.Elapsed.TotalSeconds / seconds);
                long add = (long)Math.Round(amount * t);
                _credits = startCredits + add;
                _lastWin = startWin + add;
                CreditMeter.Value = _credits;
                WinMeter.Value = _lastWin;
                shown(add);
                if (t >= 1) break;
                await Task.Delay(16);
            }
            _sound.Stop(Sfx.RollupLoop);
            _sound.Play(Sfx.RollupEnd);
            SaveState();
            _phase = previous;
            UpdateUi();
        }

        private void FlashCabinet(int times)
        {
            CabinetFlash.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(times),
            });
        }

        // ================= buttons and keys =================

        private void OnBetOne(object sender, RoutedEventArgs e)
        {
            if (_phase != Phase.Idle) return;
            _bet = _bet % SlotEngine.MaxBet + 1;
            if (_bet > _credits) _bet = (int)Math.Max(1, _credits);
            _sound.Play(Sfx.Button);
            SaveState();
            UpdateUi();
        }

        private async void OnMaxBet(object sender, RoutedEventArgs e)
        {
            if (_phase != Phase.Idle || _credits <= 0) return;
            _bet = (int)Math.Min(SlotEngine.MaxBet, _credits);
            _sound.Play(Sfx.Button);
            UpdateUi();
            await SpinPressedAsync();
        }

        private void OnInfo(object sender, RoutedEventArgs e)
        {
            _sound.Play(Sfx.Button);
            PayOverlay.Visibility = Visibility.Visible;
        }

        private void OnCloseInfo(object sender, RoutedEventArgs e) => PayOverlay.Visibility = Visibility.Collapsed;

        private void OnMute(object sender, RoutedEventArgs e)
        {
            _sound.Muted = !_sound.Muted;
            MuteLabel.Text = _sound.Muted ? "🔇" : "🔊";
        }

        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (PayOverlay.Visibility == Visibility.Visible)
            {
                if (e.Key is Key.Escape or Key.Space or Key.Enter) { PayOverlay.Visibility = Visibility.Collapsed; e.Handled = true; }
                return;
            }
            switch (e.Key)
            {
                case Key.Space:
                case Key.Enter:
                    e.Handled = true;
                    await SpinPressedAsync();
                    break;
                case Key.B:
                case Key.Up:
                    e.Handled = true;
                    OnBetOne(sender, e);
                    break;
                case Key.M:
                    e.Handled = true;
                    OnMaxBet(sender, e);
                    break;
                case Key.P:
                    e.Handled = true;
                    if (InfoButton.IsEnabled) OnInfo(sender, e);
                    break;
            }
        }
    }
}
