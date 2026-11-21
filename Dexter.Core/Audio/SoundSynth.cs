namespace Dexter.Core.Audio;

public enum SoundKind
{
    Wrong,          // неверная клавиша
    CaseMismatch,   // не тот регистр (Shift / Caps Lock)
    WrongLayout,    // не та раскладка
    ExerciseDone,   // упражнение закончено
    LessonPassed,   // урок зачтён
    LessonFailed,   // урок не зачтён
}

/// <summary>Синтез коротких WAV-звуков в памяти: внешние файлы не нужны.</summary>
public static class SoundSynth
{
    public const int SampleRate = 22050;
    public static readonly string[] Packs = { "classic", "soft", "click", "beep" };

    enum Wave { Sine, Square, Saw, Noise }
    readonly record struct Seg(double F0, double F1, int Ms, Wave Shape, double Gain, int GapMs = 0);

    static Seg[] Plan(string pack, SoundKind kind)
    {
        // Общие звуки «успеха» одинаковы во всех наборах, различаются только звуки ошибок.
        switch (kind)
        {
            case SoundKind.ExerciseDone: return new[] { new Seg(660, 660, 80, Wave.Sine, 0.35) };
            case SoundKind.LessonPassed:
                return new[]
                {
                    new Seg(523, 523, 110, Wave.Sine, 0.4, 15), new Seg(659, 659, 110, Wave.Sine, 0.4, 15),
                    new Seg(784, 784, 220, Wave.Sine, 0.4),
                };
            case SoundKind.LessonFailed:
                return new[] { new Seg(392, 349, 170, Wave.Saw, 0.3, 20), new Seg(330, 262, 260, Wave.Saw, 0.3) };
        }

        return pack switch
        {
            "soft" => kind switch
            {
                SoundKind.Wrong => new[] { new Seg(210, 120, 150, Wave.Sine, 0.7) },
                SoundKind.CaseMismatch => new[] { new Seg(330, 330, 60, Wave.Sine, 0.5, 50), new Seg(330, 330, 60, Wave.Sine, 0.5) },
                _ => new[] { new Seg(420, 200, 190, Wave.Sine, 0.55) },
            },
            "click" => kind switch
            {
                SoundKind.Wrong => new[] { new Seg(0, 0, 22, Wave.Noise, 0.55), new Seg(130, 90, 60, Wave.Sine, 0.6) },
                SoundKind.CaseMismatch => new[] { new Seg(0, 0, 18, Wave.Noise, 0.5, 60), new Seg(0, 0, 18, Wave.Noise, 0.5) },
                _ => new[] { new Seg(0, 0, 18, Wave.Noise, 0.5, 40), new Seg(200, 100, 90, Wave.Sine, 0.5) },
            },
            "beep" => kind switch
            {
                SoundKind.Wrong => new[] { new Seg(880, 880, 90, Wave.Sine, 0.4) },
                SoundKind.CaseMismatch => new[] { new Seg(988, 988, 55, Wave.Sine, 0.4, 45), new Seg(988, 988, 55, Wave.Sine, 0.4) },
                _ => new[] { new Seg(880, 660, 80, Wave.Sine, 0.4, 20), new Seg(660, 440, 110, Wave.Sine, 0.4) },
            },
            // classic: «бззт» — грубый прямоугольный гудок
            _ => kind switch
            {
                SoundKind.Wrong => new[] { new Seg(175, 140, 120, Wave.Square, 0.45) },
                SoundKind.CaseMismatch => new[] { new Seg(440, 440, 60, Wave.Square, 0.3, 45), new Seg(330, 330, 70, Wave.Square, 0.3) },
                _ => new[] { new Seg(520, 260, 180, Wave.Saw, 0.35) },
            },
        };
    }

    /// <summary>Возвращает готовый WAV (16 бит, моно). volume: 0..1.</summary>
    public static byte[] Render(string pack, SoundKind kind, double volume = 0.6)
    {
        volume = Math.Clamp(volume, 0, 1);
        var pcm = new List<short>();
        var rnd = new Random(12345);

        foreach (var seg in Plan(pack, kind))
        {
            int n = SampleRate * seg.Ms / 1000;
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n;
                phase += (seg.F0 + (seg.F1 - seg.F0) * t) / SampleRate;
                double frac = phase - Math.Floor(phase);
                double v = seg.Shape switch
                {
                    Wave.Sine => Math.Sin(2 * Math.PI * phase),
                    Wave.Square => frac < 0.5 ? 1.0 : -1.0,
                    Wave.Saw => 2 * frac - 1,
                    _ => rnd.NextDouble() * 2 - 1,
                };
                double attack = Math.Min(1.0, i / (SampleRate * 0.004));
                double release = Math.Pow(1 - t, 0.6);
                double sample = v * seg.Gain * volume * attack * release;
                pcm.Add((short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue));
            }
            pcm.AddRange(new short[SampleRate * seg.GapMs / 1000]);
        }
        return ToWav(pcm);
    }

    static byte[] ToWav(List<short> pcm)
    {
        int dataLen = pcm.Count * 2;
        using var ms = new MemoryStream(44 + dataLen);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataLen); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(SampleRate); w.Write(SampleRate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(dataLen);
        foreach (var s in pcm) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
