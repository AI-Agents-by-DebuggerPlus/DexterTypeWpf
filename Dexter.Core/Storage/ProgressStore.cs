using Dexter.Core.Typing;

namespace Dexter.Core.Storage;

public sealed class LessonProgress
{
    public int BestCpm { get; set; }
    public int BestAccuracy { get; set; }
    public int Attempts { get; set; }
    public bool Passed { get; set; }
    public DateTime? PassedAt { get; set; }
    public DateTime LastAttempt { get; set; }
}

public sealed class AttemptRecord
{
    public DateTime When { get; set; }
    public string CourseId { get; set; } = "";
    public string LessonId { get; set; } = "";
    public int Cpm { get; set; }
    public int Accuracy { get; set; }
    public int Errors { get; set; }
    public double Seconds { get; set; }
    public bool Passed { get; set; }
    /// <summary>Ошибки по ожидаемому символу (ключ — символ строкой).</summary>
    public Dictionary<string, int> Mistakes { get; set; } = new();
}

public sealed class ProgressData
{
    /// <summary>Ключ: "{courseId}/{lessonId}". Хранится без ограничения по размеру.</summary>
    public Dictionary<string, LessonProgress> Lessons { get; set; } = new();
    /// <summary>Журнал попыток; хранятся последние MaxAttempts.</summary>
    public List<AttemptRecord> Attempts { get; set; } = new();
}

public sealed class ProgressStore
{
    public const int MaxAttempts = 500;
    readonly string _path;
    readonly ProgressData _data;

    public ProgressStore(string? path = null)
    {
        _path = path ?? AppPaths.File("progress.json");
        _data = JsonFile.Load<ProgressData>(_path);
    }

    static string Key(string courseId, string lessonId) => $"{courseId}/{lessonId}";

    public LessonProgress? Get(string courseId, string lessonId) =>
        _data.Lessons.TryGetValue(Key(courseId, lessonId), out var p) ? p : null;

    public bool IsPassed(string courseId, string lessonId) => Get(courseId, lessonId)?.Passed == true;

    public IReadOnlyList<AttemptRecord> Attempts(string? courseId = null) =>
        courseId is null ? _data.Attempts : _data.Attempts.Where(a => a.CourseId == courseId).ToList();

    public AttemptRecord Record(string courseId, TypingSession s, DateTime when)
    {
        var rec = new AttemptRecord
        {
            When = when, CourseId = courseId, LessonId = s.Lesson.Id,
            Cpm = s.Cpm, Accuracy = s.Accuracy, Errors = s.Errors, Seconds = Math.Round(s.Seconds, 1),
            Passed = s.Passed,
            Mistakes = s.Mistakes.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        };
        _data.Attempts.Add(rec);
        if (_data.Attempts.Count > MaxAttempts) _data.Attempts.RemoveRange(0, _data.Attempts.Count - MaxAttempts);

        var key = Key(courseId, s.Lesson.Id);
        if (!_data.Lessons.TryGetValue(key, out var p)) _data.Lessons[key] = p = new LessonProgress();
        p.Attempts++;
        p.LastAttempt = when;
        p.BestCpm = Math.Max(p.BestCpm, rec.Cpm);
        p.BestAccuracy = Math.Max(p.BestAccuracy, rec.Accuracy);
        if (rec.Passed && !p.Passed) { p.Passed = true; p.PassedAt = when; }

        Save();
        return rec;
    }

    /// <summary>Самые «проблемные» символы по всем сохранённым попыткам курса.</summary>
    public List<(string Char, int Count)> TopMistakes(string courseId, int take)
    {
        var totals = new Dictionary<string, int>();
        foreach (var a in _data.Attempts.Where(a => a.CourseId == courseId))
            foreach (var (ch, n) in a.Mistakes)
                totals[ch] = totals.GetValueOrDefault(ch) + n;
        return totals.OrderByDescending(kv => kv.Value).Take(take).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public void ResetCourse(string courseId)
    {
        foreach (var k in _data.Lessons.Keys.Where(k => k.StartsWith(courseId + "/")).ToList()) _data.Lessons.Remove(k);
        _data.Attempts.RemoveAll(a => a.CourseId == courseId);
        Save();
    }

    void Save() => JsonFile.Save(_path, _data);
}
