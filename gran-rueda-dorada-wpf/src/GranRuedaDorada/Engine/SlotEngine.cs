using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace GranRuedaDorada.Engine
{
    /// <summary>Reel symbols. Blank is the empty space between symbols on a stepper reel.</summary>
    public enum Sym { Blank, Bar1, Bar2, Bar3, Seven, Cherry, Wild, Spin }

    public sealed record Combo(string Name, Sym[] Matches, int Pay, Sym[] Art);

    public sealed record LineWin(int Pay, string Name, int[] Reels)
    {
        public static readonly LineWin None = new(0, "", Array.Empty<int>());
    }

    public sealed record SpinResult(int Bet, int[] Stops, Sym[] Line, LineWin Win, bool Bonus);

    /// <summary>
    /// Game math for a 3-reel, 1-line stepper with a bonus wheel.
    /// Each reel has 22 physical stops; every stop carries a weight (a "virtual reel"),
    /// so blanks land more often than high symbols, as on real stepper machines.
    /// </summary>
    public sealed class SlotEngine
    {
        public const int MaxBet = 3;
        public const int StopsPerReel = 22;

        private const Sym W = Sym.Wild, X = Sym.Blank, B1 = Sym.Bar1, B2 = Sym.Bar2, B3 = Sym.Bar3,
                          S7 = Sym.Seven, CH = Sym.Cherry, SP = Sym.Spin;

        public static readonly Sym[][] Strips =
        {
            new[] { W, X, B1, X, CH, X, B2, X, S7, X, B1, X, B3, X, CH, X, B1, X, B2, X, B1, X },
            new[] { W, X, B1, X, B2, X, CH, X, S7, X, B1, X, B3, X, B1, X, B2, X, CH, X, B1, X },
            new[] { W, X, B1, X, SP, X, B2, X, S7, X, B1, X, B3, X, CH, X, B1, X, B2, X, SP, X },
        };

        public static double StopWeight(Sym s) => s switch
        {
            Sym.Blank => 3,
            Sym.Bar1 => 3,
            Sym.Bar2 => 2,
            Sym.Bar3 => 2,
            Sym.Seven => 1,
            Sym.Wild => 1,
            Sym.Cherry => 2,
            Sym.Spin => 0.6,
            _ => 1,
        };

        /// <summary>Three wilds pay this, per bet level (1, 2, 3 credits). Max bet gets the boosted top award.</summary>
        public static readonly int[] TopAward = { 800, 1600, 5000 };

        public static readonly Combo[] Combos =
        {
            new("3 Sietes", new[] { S7 }, 80, new[] { S7, S7, S7 }),
            new("3 Triple BAR", new[] { B3 }, 40, new[] { B3, B3, B3 }),
            new("3 Doble BAR", new[] { B2 }, 20, new[] { B2, B2, B2 }),
            new("3 BAR", new[] { B1 }, 10, new[] { B1, B1, B1 }),
            new("3 Cerezas", new[] { CH }, 10, new[] { CH, CH, CH }),
            new("3 BAR mixtas", new[] { B1, B2, B3 }, 2, new[] { B1, B2, B3 }),
        };

        public const int TwoCherries = 5;
        public const int OneCherry = 2;

        /// <summary>Bonus wheel values, clockwise from the pointer at rest.</summary>
        public static readonly int[] WheelValues =
            { 1000, 25, 100, 50, 200, 30, 75, 300, 40, 150, 500, 25, 120, 60, 250, 35, 80, 400, 45, 100, 750, 50 };

        /// <summary>Segment probability falls steeply with value: 25 is common, 1000 is rare.</summary>
        public static readonly double[] WheelWeights = WheelValues.Select(v => 1.0 / Math.Pow(v, 1.8)).ToArray();

        private readonly Func<double> _random;

        /// <param name="random">Uniform [0,1) source. Defaults to a cryptographic RNG.</param>
        public SlotEngine(Func<double>? random = null)
        {
            _random = random ?? (() => RandomNumberGenerator.GetInt32(int.MaxValue) / (double)int.MaxValue);
        }

        public SpinResult Spin(int bet)
        {
            if (bet < 1 || bet > MaxBet) throw new ArgumentOutOfRangeException(nameof(bet));
            var stops = new int[3];
            var line = new Sym[3];
            for (int r = 0; r < 3; r++)
            {
                stops[r] = WeightedPick(Strips[r].Select(StopWeight).ToArray());
                line[r] = Strips[r][stops[r]];
            }
            return new SpinResult(bet, stops, line, Evaluate(line, bet), IsBonus(line, bet));
        }

        public int PickWheelSegment() => WeightedPick(WheelWeights);

        public static bool IsBonus(Sym[] line, int bet) => bet == MaxBet && line[2] == Sym.Spin;

        /// <summary>Evaluates the single payline. Wild substitutes for everything but Spin and doubles the win per wild.</summary>
        public static LineWin Evaluate(IReadOnlyList<Sym> line, int bet)
        {
            int wilds = line.Count(s => s == Sym.Wild);
            if (wilds == 3) return new LineWin(TopAward[bet - 1], "3 Comodines", new[] { 0, 1, 2 });

            foreach (var c in Combos)
            {
                if (line.All(s => s == Sym.Wild || c.Matches.Contains(s)))
                {
                    int mult = 1 << wilds;
                    string name = mult > 1 ? $"{c.Name} ×{mult}" : c.Name;
                    return new LineWin(c.Pay * bet * mult, name, new[] { 0, 1, 2 });
                }
            }

            int cherries = line.Count(s => s == Sym.Cherry);
            if (cherries > 0)
            {
                int k = cherries + wilds;
                var reels = Enumerable.Range(0, 3).Where(i => line[i] == Sym.Cherry || line[i] == Sym.Wild).ToArray();
                return k >= 2
                    ? new LineWin(TwoCherries * bet, "2 Cerezas", reels)
                    : new LineWin(OneCherry * bet, "1 Cereza", reels);
            }
            return LineWin.None;
        }

        public static double WheelExpectedValue()
        {
            double total = WheelWeights.Sum();
            return WheelValues.Select((v, i) => v * WheelWeights[i]).Sum() / total;
        }

        /// <summary>Exact return-to-player for a bet level, by enumerating every stop combination.</summary>
        public static (double Rtp, double HitRate, double BonusRate) Analyze(int bet)
        {
            var weights = Strips.Select(s => s.Select(StopWeight).ToArray()).ToArray();
            var totals = weights.Select(w => w.Sum()).ToArray();
            double wheel = WheelExpectedValue();
            double ret = 0, hit = 0, bonus = 0;
            var line = new Sym[3];
            for (int a = 0; a < StopsPerReel; a++)
            for (int b = 0; b < StopsPerReel; b++)
            for (int c = 0; c < StopsPerReel; c++)
            {
                double p = weights[0][a] * weights[1][b] * weights[2][c] / (totals[0] * totals[1] * totals[2]);
                line[0] = Strips[0][a]; line[1] = Strips[1][b]; line[2] = Strips[2][c];
                double pay = Evaluate(line, bet).Pay;
                if (IsBonus(line, bet)) { pay += wheel; bonus += p; }
                if (pay > 0) hit += p;
                ret += pay * p;
            }
            return (ret / bet, hit, bonus);
        }

        private int WeightedPick(double[] weights)
        {
            double r = _random() * weights.Sum();
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r < 0) return i;
            }
            return weights.Length - 1;
        }
    }
}
