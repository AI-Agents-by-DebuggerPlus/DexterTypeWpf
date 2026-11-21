namespace Dexter.Core.Courses;

/// <summary>Критерий зачёта урока.</summary>
public sealed class Criteria
{
    public int MinCpm { get; set; } = 100;       // знаков в минуту
    public int MinAccuracy { get; set; } = 96;   // процентов
}

public sealed class Lesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Клавиши, которые вводятся в этом уроке (для справки в интерфейсе).</summary>
    public string NewKeys { get; set; } = "";
    /// <summary>Короткое пояснение перед уроком (постановка пальцев, правила).</summary>
    public string Intro { get; set; } = "";
    /// <summary>Строки-упражнения; урок проходится целиком, статистика общая.</summary>
    public List<string> Exercises { get; set; } = new();
    public int? MinCpm { get; set; }
    public int? MinAccuracy { get; set; }
}

public sealed class Part
{
    public string Title { get; set; } = "";
    public List<Lesson> Lessons { get; set; } = new();
}

public sealed class Course
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Id раскладки: "ru" или "en".</summary>
    public string Layout { get; set; } = "ru";
    public Criteria Criteria { get; set; } = new();
    public List<Part> Parts { get; set; } = new();

    public IEnumerable<Lesson> AllLessons() => Parts.SelectMany(p => p.Lessons);

    public Criteria CriteriaFor(Lesson lesson) => new()
    {
        MinCpm = lesson.MinCpm ?? Criteria.MinCpm,
        MinAccuracy = lesson.MinAccuracy ?? Criteria.MinAccuracy,
    };
}
