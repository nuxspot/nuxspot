using System;
using System.IO;
using System.Text;

namespace GranRuedaDorada.Audio
{
    /// <summary>
    /// Generates every sound effect from scratch (bells, reel mechanics, fanfares, applause)
    /// so the game ships without audio files. Output is 16-bit mono PCM WAV.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;
        private static readonly Random Noise = new(1234);

        private static double Midi(int note) => 440 * Math.Pow(2, (note - 69) / 12.0);
        private static double Rnd() => Noise.NextDouble() * 2 - 1;

        private static float[] Buffer(double seconds) => new float[(int)(seconds * Rate)];

        /// <summary>Struck bell: inharmonic partials, each decaying at its own rate.</summary>
        private static void Bell(float[] b, double start, double freq, double amp, double decay = 5)
        {
            double[] ratios = { 1, 2.0, 2.76, 4.07, 5.4 };
            double[] amps = { 1, 0.55, 0.4, 0.22, 0.15 };
            int s0 = (int)(start * Rate);
            int len = Math.Min(b.Length - s0, (int)(2.5 * Rate));
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate, v = 0;
                for (int p = 0; p < ratios.Length; p++)
                    v += amps[p] * Math.Sin(2 * Math.PI * freq * ratios[p] * t) * Math.Exp(-t * decay * (1 + p * 0.6));
                double attack = Math.Min(1, t / 0.002);
                b[s0 + i] += (float)(v * amp * attack * 0.35);
            }
        }

        /// <summary>Brass-like tone: band-limited sawtooth with vibrato and a soft attack.</summary>
        private static void Brass(float[] b, double start, double dur, double freq, double amp)
        {
            int s0 = (int)(start * Rate), len = Math.Min(b.Length - s0, (int)((dur + 0.25) * Rate));
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate;
                double vib = 1 + 0.006 * Math.Sin(2 * Math.PI * 5.5 * t) * Math.Min(1, t / 0.2);
                phase += 2 * Math.PI * freq * vib / Rate;
                double v = 0;
                for (int k = 1; k <= 9; k++) v += Math.Sin(phase * k) / k * (k <= 3 ? 1 : 0.7);
                double env = Math.Min(1, t / 0.035) * (t < dur ? 1 - 0.25 * (t / dur) : Math.Exp(-(t - dur) * 14) * 0.75);
                b[s0 + i] += (float)(v * env * amp * 0.25);
            }
        }

        private static void ClickAt(float[] b, double start, double amp, double decay = 900)
        {
            int s0 = (int)(start * Rate), len = Math.Min(b.Length - s0, (int)(0.03 * Rate));
            double prev = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate, n = Rnd();
                double hp = n - prev; prev = n; // crude high-pass: a sharper, plastic tick
                b[s0 + i] += (float)(hp * Math.Exp(-t * decay) * amp);
            }
        }

        private static void Thump(float[] b, double start, double amp)
        {
            int s0 = (int)(start * Rate), len = Math.Min(b.Length - s0, (int)(0.25 * Rate));
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate;
                phase += 2 * Math.PI * (95 - 45 * Math.Min(1, t / 0.08)) / Rate;
                b[s0 + i] += (float)(Math.Sin(phase) * Math.Exp(-t * 22) * amp);
            }
            ClickAt(b, start, amp * 0.5, 500);
        }

        /// <summary>Simple feedback delay that gives the cabinet some room sound.</summary>
        private static void Room(float[] b, double delay = 0.083, double feedback = 0.28)
        {
            int d = (int)(delay * Rate), d2 = (int)(delay * 1.37 * Rate);
            for (int i = d2; i < b.Length; i++) b[i] += (float)(b[i - d] * feedback + b[i - d2] * feedback * 0.6);
        }

        private static float[] Normalize(float[] b, double peak = 0.9)
        {
            float max = 0;
            foreach (var v in b) max = Math.Max(max, Math.Abs(v));
            if (max > 0) for (int i = 0; i < b.Length; i++) b[i] = (float)(b[i] / max * peak);
            return b;
        }

        // ---------- the sound set ----------

        public static float[] Button()
        {
            var b = Buffer(0.12);
            ClickAt(b, 0, 0.6, 600);
            Bell(b, 0, 1568, 0.25, 30);
            return Normalize(b, 0.6);
        }

        /// <summary>Reel motor and ratchet; exactly one second so it loops without drift.</summary>
        public static float[] ReelLoop()
        {
            var b = Buffer(1.0);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                phase += 2 * Math.PI * 60 / Rate;
                double saw = 0;
                for (int k = 1; k <= 6; k++) saw += Math.Sin(phase * k) / k;
                b[i] += (float)(saw * 0.18 + Rnd() * 0.04);
            }
            for (int k = 0; k < 24; k++) ClickAt(b, k / 24.0, 0.5, 1400);
            return Normalize(b, 0.5);
        }

        public static float[] ReelStart()
        {
            var b = Buffer(0.3);
            Thump(b, 0, 0.7);
            ClickAt(b, 0.04, 0.6);
            return Normalize(b, 0.7);
        }

        public static float[] ReelStop()
        {
            var b = Buffer(0.35);
            Thump(b, 0, 1);
            ClickAt(b, 0.012, 0.8, 700);
            return Normalize(b, 0.95);
        }

        /// <summary>Rising, trembling tone while the last reel is held for anticipation.</summary>
        public static float[] Anticipation()
        {
            var b = Buffer(2.4);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double t = (double)i / Rate;
                double f = 220 * Math.Pow(2, t / 1.2);
                phase += 2 * Math.PI * f / Rate;
                double trem = 0.55 + 0.45 * Math.Sin(2 * Math.PI * (8 + t * 6) * t);
                double v = Math.Sin(phase) + 0.4 * Math.Sin(phase * 2) + 0.2 * Math.Sin(phase * 3);
                b[i] = (float)(v * trem * Math.Min(1, t / 0.1) * 0.3);
            }
            Room(b);
            return Normalize(b, 0.6);
        }

        public static float[] WheelTick()
        {
            var b = Buffer(0.06);
            ClickAt(b, 0, 1, 1100);
            Bell(b, 0, 2637, 0.2, 60);
            return Normalize(b, 0.8);
        }

        public static float[] SmallWin()
        {
            var b = Buffer(1.4);
            int[] notes = { 72, 76, 79, 84 };
            for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.08, Midi(notes[i]), 1, 4);
            Room(b);
            return Normalize(b, 0.8);
        }

        /// <summary>Continuous "ding-ding" of the credit meter counting up; one second, loops seamlessly.</summary>
        public static float[] RollupLoop()
        {
            // Render two seconds, then fold the ringing tail back onto the start so the loop has no seam.
            var b = Buffer(2.0);
            for (int k = 0; k < 16; k++) Bell(b, k / 16.0, Midi(k % 2 == 0 ? 88 : 91), 0.8, 14);
            var loop = new float[Rate];
            for (int i = 0; i < Rate; i++) loop[i] = b[i] + b[i + Rate];
            return Normalize(loop, 0.55);
        }

        public static float[] RollupEnd()
        {
            var b = Buffer(1.6);
            Bell(b, 0, Midi(84), 1, 2.5);
            Bell(b, 0, Midi(91), 0.7, 2.5);
            Room(b);
            return Normalize(b, 0.8);
        }

        /// <summary>Triumphant brass fanfare for big wins.</summary>
        public static float[] BigWin()
        {
            var b = Buffer(3.6);
            (double t, double d, int n)[] melody =
            {
                (0.00, 0.14, 67), (0.15, 0.14, 67), (0.30, 0.14, 67), (0.45, 0.55, 72),
                (1.05, 0.14, 76), (1.20, 0.14, 74), (1.35, 0.14, 76), (1.50, 1.3, 79),
            };
            foreach (var (t, d, n) in melody)
            {
                Brass(b, t, d, Midi(n), 1);
                Brass(b, t, d, Midi(n - 12), 0.5);
            }
            foreach (var n in new[] { 60, 64, 67, 72 }) Brass(b, 1.5, 1.3, Midi(n), 0.35);
            for (int i = 0; i < 10; i++) Bell(b, 1.5 + i * 0.09, Midi(84 + (i % 3) * 4), 0.35, 5);
            Room(b);
            return Normalize(b, 0.92);
        }

        /// <summary>Ascending run into a fanfare when the wheel bonus is triggered.</summary>
        public static float[] BonusTrigger()
        {
            var b = Buffer(3.0);
            for (int i = 0; i < 16; i++) Bell(b, i * 0.045, Midi(60 + i * 2), 0.6, 8);
            (double t, double d, int n)[] melody = { (0.8, 0.18, 72), (1.0, 0.18, 76), (1.2, 0.18, 79), (1.4, 1.2, 84) };
            foreach (var (t, d, n) in melody) { Brass(b, t, d, Midi(n), 1); Brass(b, t, d, Midi(n - 7), 0.45); }
            Room(b);
            return Normalize(b, 0.92);
        }

        public static float[] WheelStart()
        {
            var b = Buffer(1.2);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double t = (double)i / Rate;
                phase += 2 * Math.PI * (180 + 900 * t * t) / Rate;
                b[i] = (float)(Math.Sin(phase) * Math.Exp(-t * 2.2) * 0.4 + Rnd() * 0.15 * Math.Exp(-t * 4));
            }
            Room(b);
            return Normalize(b, 0.6);
        }

        /// <summary>Crowd applause: hundreds of individual noise-burst claps with a swell and fade.</summary>
        public static float[] Applause()
        {
            var b = Buffer(3.8);
            int claps = 1400;
            for (int k = 0; k < claps; k++)
            {
                double t = Noise.NextDouble() * 3.6;
                double env = Math.Min(1, t / 0.4) * (t > 2.2 ? Math.Max(0, 1 - (t - 2.2) / 1.4) : 1);
                if (Noise.NextDouble() > env) continue;
                double amp = 0.2 + Noise.NextDouble() * 0.5;
                double decay = 250 + Noise.NextDouble() * 400;
                ClickAt(b, t, amp, decay);
            }
            Room(b, 0.05, 0.3);
            return Normalize(b, 0.7);
        }

        public static float[] Credit()
        {
            var b = Buffer(0.9);
            Bell(b, 0, Midi(79), 1, 6);
            Bell(b, 0.1, Midi(86), 1, 6);
            return Normalize(b, 0.7);
        }

        public static byte[] ToWav(float[] samples)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.ASCII);
            int dataLen = samples.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);          // PCM
            w.Write((short)1);          // mono
            w.Write(Rate);
            w.Write(Rate * 2);          // byte rate
            w.Write((short)2);          // block align
            w.Write((short)16);         // bits per sample
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            foreach (var s in samples) w.Write((short)Math.Clamp(s * 32767, -32768, 32767));
            w.Flush();
            return ms.ToArray();
        }
    }
}
