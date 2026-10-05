using System;
using System.Linq;
using System.Text;
using GranRuedaDorada.Audio;
using Xunit;

namespace GranRuedaDorada.Tests
{
    public class SynthTests
    {
        public static TheoryData<string> Sounds => new()
        {
            "Button", "ReelLoop", "ReelStart", "ReelStop", "Anticipation", "WheelTick", "SmallWin",
            "RollupLoop", "RollupEnd", "BigWin", "BonusTrigger", "WheelStart", "Applause", "Credit",
        };

        [Theory]
        [MemberData(nameof(Sounds))]
        public void EverySoundIsFiniteAudibleAndUnclipped(string name)
        {
            var samples = (float[])typeof(Synth).GetMethod(name)!.Invoke(null, null)!;
            Assert.NotEmpty(samples);
            Assert.All(samples, s => Assert.True(float.IsFinite(s)));
            float peak = samples.Max(Math.Abs);
            Assert.InRange(peak, 0.3f, 1.0f);
        }

        [Fact]
        public void LoopsAreExactlyOneSecond()
        {
            Assert.Equal(Synth.Rate, Synth.ReelLoop().Length);
            Assert.Equal(Synth.Rate, Synth.RollupLoop().Length);
        }

        [Fact]
        public void WavHeaderIsValidPcm()
        {
            var wav = Synth.ToWav(new float[] { 0, 0.5f, -0.5f, 1 });
            Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
            Assert.Equal(44 + 8, wav.Length);
            Assert.Equal(Synth.Rate, BitConverter.ToInt32(wav, 24));
            Assert.Equal(32767, BitConverter.ToInt16(wav, 50));
        }
    }
}
