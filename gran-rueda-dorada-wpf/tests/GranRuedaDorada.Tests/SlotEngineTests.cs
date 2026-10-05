using System;
using System.Linq;
using GranRuedaDorada.Engine;
using Xunit;

namespace GranRuedaDorada.Tests
{
    public class SlotEngineTests
    {
        private static Sym[] L(Sym a, Sym b, Sym c) => new[] { a, b, c };

        [Fact]
        public void ThreeWildsPayTheTopAwardForEachBetLevel()
        {
            Assert.Equal(800, SlotEngine.Evaluate(L(Sym.Wild, Sym.Wild, Sym.Wild), 1).Pay);
            Assert.Equal(5000, SlotEngine.Evaluate(L(Sym.Wild, Sym.Wild, Sym.Wild), 3).Pay);
        }

        [Theory]
        [InlineData(Sym.Seven, 80)]
        [InlineData(Sym.Bar3, 40)]
        [InlineData(Sym.Bar2, 20)]
        [InlineData(Sym.Bar1, 10)]
        [InlineData(Sym.Cherry, 10)]
        public void ThreeOfAKindPaysPerCredit(Sym s, int pay)
        {
            Assert.Equal(pay * 2, SlotEngine.Evaluate(L(s, s, s), 2).Pay);
        }

        [Fact]
        public void WildsDoubleAndQuadruple()
        {
            Assert.Equal(160, SlotEngine.Evaluate(L(Sym.Seven, Sym.Wild, Sym.Seven), 1).Pay);
            Assert.Equal(320, SlotEngine.Evaluate(L(Sym.Wild, Sym.Seven, Sym.Wild), 1).Pay);
        }

        [Fact]
        public void MixedBarsPayAnyBar()
        {
            var win = SlotEngine.Evaluate(L(Sym.Bar1, Sym.Bar3, Sym.Bar2), 3);
            Assert.Equal(6, win.Pay);
            Assert.Equal("3 BAR mixtas", win.Name);
        }

        [Fact]
        public void HighestComboWinsOverMixedBars()
        {
            Assert.Equal("3 Triple BAR ×2", SlotEngine.Evaluate(L(Sym.Bar3, Sym.Wild, Sym.Bar3), 1).Name);
        }

        [Fact]
        public void CherriesPayAnywhereOnTheLine()
        {
            Assert.Equal(2, SlotEngine.Evaluate(L(Sym.Blank, Sym.Blank, Sym.Cherry), 1).Pay);
            Assert.Equal(5, SlotEngine.Evaluate(L(Sym.Cherry, Sym.Blank, Sym.Cherry), 1).Pay);
            Assert.Equal(new[] { 0, 2 }, SlotEngine.Evaluate(L(Sym.Cherry, Sym.Blank, Sym.Cherry), 1).Reels);
        }

        [Fact]
        public void SpinSymbolIsNotSubstitutedAndBlanksPayNothing()
        {
            Assert.Equal(0, SlotEngine.Evaluate(L(Sym.Wild, Sym.Wild, Sym.Spin), 3).Pay);
            Assert.Equal(0, SlotEngine.Evaluate(L(Sym.Seven, Sym.Blank, Sym.Seven), 3).Pay);
        }

        [Fact]
        public void BonusNeedsMaxBetAndSpinOnTheThirdReel()
        {
            Assert.True(SlotEngine.IsBonus(L(Sym.Blank, Sym.Blank, Sym.Spin), 3));
            Assert.False(SlotEngine.IsBonus(L(Sym.Blank, Sym.Blank, Sym.Spin), 2));
            Assert.DoesNotContain(Sym.Spin, SlotEngine.Strips[0]);
            Assert.DoesNotContain(Sym.Spin, SlotEngine.Strips[1]);
        }

        [Fact]
        public void MaxBetReturnIsInARealisticCasinoRange()
        {
            var (rtp, hit, bonus) = SlotEngine.Analyze(3);
            Assert.InRange(rtp, 0.92, 0.97);
            Assert.InRange(hit, 0.15, 0.30);
            Assert.InRange(1 / bonus, 30, 60);
        }

        [Fact]
        public void SpinResultMatchesTheStrips()
        {
            var rng = new Random(7);
            var engine = new SlotEngine(rng.NextDouble);
            for (int i = 0; i < 2000; i++)
            {
                var r = engine.Spin(3);
                for (int reel = 0; reel < 3; reel++) Assert.Equal(SlotEngine.Strips[reel][r.Stops[reel]], r.Line[reel]);
                var expected = SlotEngine.Evaluate(r.Line, 3);
                Assert.Equal(expected.Pay, r.Win.Pay);
                Assert.Equal(expected.Name, r.Win.Name);
            }
        }

        [Fact]
        public void SimulatedPlayConvergesOnTheTheoreticalReturn()
        {
            var rng = new Random(42);
            var engine = new SlotEngine(rng.NextDouble);
            double wheel = SlotEngine.WheelExpectedValue();
            double paid = 0;
            const int spins = 400_000;
            for (int i = 0; i < spins; i++)
            {
                var r = engine.Spin(3);
                paid += r.Win.Pay + (r.Bonus ? SlotEngine.WheelValues[engine.PickWheelSegment()] : 0);
            }
            Assert.InRange(paid / (spins * 3.0), SlotEngine.Analyze(3).Rtp - 0.03, SlotEngine.Analyze(3).Rtp + 0.03);
            Assert.True(wheel > 25 && wheel < 1000);
        }

        [Fact]
        public void WheelPicksAreAlwaysValidSegments()
        {
            var engine = new SlotEngine(new Random(3).NextDouble);
            var seen = Enumerable.Range(0, 20000).Select(_ => engine.PickWheelSegment()).ToHashSet();
            Assert.All(seen, i => Assert.InRange(i, 0, SlotEngine.WheelValues.Length - 1));
            Assert.Contains(Array.IndexOf(SlotEngine.WheelValues, 25), seen);
        }
    }
}
