namespace DexterType;

/// <summary>Логика тренировки без UI: генерация текста и метрики. Готово к переносу в Dexter.Core.</summary>
public sealed class TrainerEngine
{
    readonly Random _r = new();
    DateTime? _start, _end;

    public Layout Current { get; private set; } = Layouts.Ru;
    public int Lesson { get; private set; }
    public string Text { get; private set; } = "";
    public int Pos { get; private set; }
    public int Correct { get; private set; }
    public int Errors { get; private set; }

    public bool Done => Text.Length > 0 && Pos >= Text.Length;
    public double Seconds => _start is null ? 0 : ((_end ?? DateTime.UtcNow) - _start.Value).TotalSeconds;
    public int Cpm => Seconds > 1 ? (int)Math.Round(Correct / Seconds * 60) : 0;
    public int Accuracy => Correct + Errors == 0 ? 100 : (int)Math.Round(100.0 * Correct / (Correct + Errors));

    public void Start(Layout layout, int lesson)
    {
        Current = layout; Lesson = lesson; Text = Generate();
        Pos = Correct = Errors = 0; _start = _end = null;
    }

    /// <summary>Возвращает true, если нажата верная клавиша. Ошибка не продвигает текст.</summary>
    public bool Press(char c)
    {
        if (Done) return false;
        _start ??= DateTime.UtcNow;
        if (char.ToLowerInvariant(c) == Text[Pos])
        {
            Correct++; Pos++;
            if (Done) _end = DateTime.UtcNow;
            return true;
        }
        Errors++;
        return false;
    }

    string Generate()
    {
        var words = new List<string>(); int len = 0;
        if (Lesson >= Current.Groups.Length)
        {
            while (len < 70) { var w = Current.Words[_r.Next(Current.Words.Length)]; words.Add(w); len += w.Length + 1; }
            return string.Join(' ', words);
        }
        string set = string.Concat(Current.Groups.Take(Lesson + 1)), fresh = Current.Groups[Lesson];
        while (len < 70)
        {
            var w = fresh[_r.Next(fresh.Length)].ToString();
            for (int i = _r.Next(1, 6); i > 0; i--)
            {
                var pool = _r.Next(10) < 4 ? fresh : set;
                w += pool[_r.Next(pool.Length)];
            }
            words.Add(w); len += w.Length + 1;
        }
        return string.Join(' ', words);
    }
}
