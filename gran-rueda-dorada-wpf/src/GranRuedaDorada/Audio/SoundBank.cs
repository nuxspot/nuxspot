using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GranRuedaDorada.Audio
{
    public enum Sfx
    {
        Button, ReelStart, ReelLoop, ReelStop, Anticipation, WheelStart, WheelTick,
        SmallWin, RollupLoop, RollupEnd, BigWin, BonusTrigger, Applause, Credit,
    }

    /// <summary>
    /// Synthesizes the sound set once into WAV files under %TEMP% and plays them through pooled
    /// MediaPlayer instances, so several sounds can overlap (reel ratchet, bells, applause).
    /// Must be used from the UI thread.
    /// </summary>
    public sealed class SoundBank
    {
        private sealed class Voice
        {
            public readonly List<MediaPlayer> Players = new();
            public int Next;
            public bool Looping;
        }

        private readonly Dictionary<Sfx, Voice> _voices = new();
        private bool _muted;

        public bool Ready { get; private set; }

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                foreach (var v in _voices.Values)
                    foreach (var p in v.Players) p.IsMuted = value;
            }
        }

        public async Task InitializeAsync()
        {
            var dir = Path.Combine(Path.GetTempPath(), "GranRuedaDorada", "sfx-v1");
            Directory.CreateDirectory(dir);

            var makers = new Dictionary<Sfx, Func<float[]>>
            {
                [Sfx.Button] = Synth.Button,
                [Sfx.ReelStart] = Synth.ReelStart,
                [Sfx.ReelLoop] = Synth.ReelLoop,
                [Sfx.ReelStop] = Synth.ReelStop,
                [Sfx.Anticipation] = Synth.Anticipation,
                [Sfx.WheelStart] = Synth.WheelStart,
                [Sfx.WheelTick] = Synth.WheelTick,
                [Sfx.SmallWin] = Synth.SmallWin,
                [Sfx.RollupLoop] = Synth.RollupLoop,
                [Sfx.RollupEnd] = Synth.RollupEnd,
                [Sfx.BigWin] = Synth.BigWin,
                [Sfx.BonusTrigger] = Synth.BonusTrigger,
                [Sfx.Applause] = Synth.Applause,
                [Sfx.Credit] = Synth.Credit,
            };

            // Synthesis is CPU work; do it off the UI thread, then create players back on it.
            var files = await Task.Run(() =>
            {
                var result = new Dictionary<Sfx, string>();
                foreach (var (sfx, make) in makers)
                {
                    var path = Path.Combine(dir, sfx + ".wav");
                    if (!File.Exists(path)) File.WriteAllBytes(path, Synth.ToWav(make()));
                    result[sfx] = path;
                }
                return result;
            });

            foreach (var (sfx, path) in files)
            {
                // Rapid-fire sounds need several players so a new hit doesn't cut off the last one.
                int count = sfx switch { Sfx.WheelTick => 6, Sfx.ReelStop => 3, Sfx.Button => 3, _ => 1 };
                var voice = new Voice();
                for (int i = 0; i < count; i++)
                {
                    var p = new MediaPlayer { Volume = sfx == Sfx.ReelLoop ? 0.55 : 0.9, IsMuted = _muted };
                    p.Open(new Uri(path));
                    voice.Players.Add(p);
                }
                if (sfx is Sfx.ReelLoop or Sfx.RollupLoop)
                {
                    var p = voice.Players[0];
                    p.MediaEnded += (_, _) =>
                    {
                        if (!voice.Looping) return;
                        p.Position = TimeSpan.Zero;
                        p.Play();
                    };
                }
                _voices[sfx] = voice;
            }
            Ready = true;
        }

        public void Play(Sfx sfx)
        {
            if (!_voices.TryGetValue(sfx, out var v)) return;
            try
            {
                var p = v.Players[v.Next];
                v.Next = (v.Next + 1) % v.Players.Count;
                p.Stop();
                p.Position = TimeSpan.Zero;
                p.Play();
            }
            catch (InvalidOperationException)
            {
                // Media playback unavailable (e.g. no audio device); the game runs silently.
            }
        }

        public void Loop(Sfx sfx)
        {
            if (!_voices.TryGetValue(sfx, out var v) || v.Looping) return;
            v.Looping = true;
            Play(sfx);
        }

        public void Stop(Sfx sfx)
        {
            if (!_voices.TryGetValue(sfx, out var v)) return;
            v.Looping = false;
            foreach (var p in v.Players) p.Stop();
        }
    }
}
