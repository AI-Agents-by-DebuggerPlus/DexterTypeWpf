using Dexter.Core.Courses;
using Dexter.Core.Keyboard;

namespace Dexter.Core.Typing;

public enum KeyOutcome
{
    Ignored,       // управляющий символ или урок уже завершён
    Correct,
    WrongKey,      // не та клавиша
    CaseMismatch,  // нужная буква, но не тот регистр (Shift / Caps Lock)
    WrongLayout,   // символ вообще не из раскладки курса (например, латиница в русском уроке)
}

/// <summary>Одна попытка прохождения урока: набор упражнений подряд, общая статистика. Без UI.</summary>
public sealed class TypingSession
{
    readonly KeyboardLayoutDef _layout;
    readonly Func<DateTime> _now;
    readonly int[] _prefix;                        // сумма длин предыдущих упражнений
    readonly Dictionary<char, int> _mistakes = new();
    DateTime? _start, _end;

    public TypingSession(Lesson lesson, Criteria criteria, KeyboardLayoutDef layout, Func<DateTime>? now = null)
    {
        if (lesson.Exercises.Count == 0) throw new ArgumentException("В уроке нет упражнений.", nameof(lesson));
        Lesson = lesson; Criteria = criteria; _layout = layout; _now = now ?? (() => DateTime.UtcNow);

        _prefix = new int[lesson.Exercises.Count + 1];
        for (int i = 0; i < lesson.Exercises.Count; i++) _prefix[i + 1] = _prefix[i] + lesson.Exercises[i].Length;
        TotalLength = _prefix[^1];
        Text = lesson.Exercises[0];
    }

    public Lesson Lesson { get; }
    public Criteria Criteria { get; }
    public int ExerciseIndex { get; private set; }
    public int ExerciseCount => Lesson.Exercises.Count;
    public string Text { get; private set; }
    public int Pos { get; private set; }
    public int Correct { get; private set; }
    public int Errors { get; private set; }
    public int TotalLength { get; }
    public bool Finished { get; private set; }
    public bool Started => _start is not null;

    public char? Expected => Finished ? null : Text[Pos];
    public double ProgressFraction => TotalLength == 0 ? 0 : (double)(_prefix[ExerciseIndex] + Pos) / TotalLength;

    public double Seconds => _start is null ? 0 : ((_end ?? _now()) - _start.Value).TotalSeconds;
    public int Cpm => Seconds >= 1 ? (int)Math.Round(Correct / Seconds * 60) : 0;

    /// <summary>Точность в целых процентах, округление вниз (чтобы «96%» на экране всегда означало ≥ 96).</summary>
    public int Accuracy => Correct + Errors == 0 ? 100 : (int)Math.Floor(100.0 * Correct / (Correct + Errors));

    public bool Passed => Finished && Cpm >= Criteria.MinCpm && Accuracy >= Criteria.MinAccuracy;

    /// <summary>Ошибки по ожидаемому символу (какие клавиши даются хуже всего).</summary>
    public IReadOnlyDictionary<char, int> Mistakes => _mistakes;

    public KeyOutcome Press(char c)
    {
        if (Finished || char.IsControl(c)) return KeyOutcome.Ignored;
        _start ??= _now();

        char expected = Text[Pos];
        if (c == expected)
        {
            Correct++; Pos++;
            if (Pos >= Text.Length) AdvanceExercise();
            return KeyOutcome.Correct;
        }

        Errors++;
        _mistakes[expected] = _mistakes.GetValueOrDefault(expected) + 1;

        if (c != expected && char.ToLowerInvariant(c) == char.ToLowerInvariant(expected)) return KeyOutcome.CaseMismatch;
        if (char.IsLetter(c) && !_layout.Contains(c)) return KeyOutcome.WrongLayout;
        return KeyOutcome.WrongKey;
    }

    void AdvanceExercise()
    {
        if (ExerciseIndex + 1 < ExerciseCount)
        {
            ExerciseIndex++;
            Text = Lesson.Exercises[ExerciseIndex];
            Pos = 0;
        }
        else
        {
            Finished = true;
            _end = _now();
        }
    }
}
