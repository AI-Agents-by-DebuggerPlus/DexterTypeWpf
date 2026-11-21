using System.Text.Json;
using Dexter.Core.Keyboard;

namespace Dexter.Core.Courses;

public sealed record CourseLoadResult(List<Course> Courses, List<string> Errors);

public static class CourseLoader
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Course LoadFile(string path) => Parse(File.ReadAllText(path));

    public static Course Parse(string json)
    {
        var course = JsonSerializer.Deserialize<Course>(json, Options)
                     ?? throw new InvalidDataException("Пустой файл курса.");
        Validate(course);
        return course;
    }

    /// <summary>Загружает все *.json из папок; повторяющиеся Id пропускаются (первая папка важнее).</summary>
    public static CourseLoadResult LoadDirectories(params string[] dirs)
    {
        var result = new CourseLoadResult(new(), new());
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var c = LoadFile(file);
                    if (result.Courses.All(x => x.Id != c.Id)) result.Courses.Add(c);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }
        return result;
    }

    public static void Validate(Course c)
    {
        if (string.IsNullOrWhiteSpace(c.Id)) throw new InvalidDataException("У курса нет id.");
        if (string.IsNullOrWhiteSpace(c.Title)) throw new InvalidDataException($"Курс {c.Id}: нет названия.");
        if (KeyboardLayouts.TryById(c.Layout) is null) throw new InvalidDataException($"Курс {c.Id}: неизвестная раскладка «{c.Layout}».");

        var ids = new HashSet<string>();
        foreach (var l in c.AllLessons())
        {
            if (string.IsNullOrWhiteSpace(l.Id)) throw new InvalidDataException($"Курс {c.Id}: урок без id.");
            if (!ids.Add(l.Id)) throw new InvalidDataException($"Курс {c.Id}: повторяется id урока {l.Id}.");
            if (l.Exercises.Count == 0 || l.Exercises.Any(string.IsNullOrEmpty))
                throw new InvalidDataException($"Урок {l.Id}: нет упражнений или есть пустое упражнение.");
        }
        if (ids.Count == 0) throw new InvalidDataException($"Курс {c.Id}: нет уроков.");
    }

    /// <summary>Находит символы, которые нельзя набрать на раскладке курса (для проверки данных).</summary>
    public static List<string> CheckTypeable(Course c)
    {
        var layout = KeyboardLayouts.ById(c.Layout);
        var problems = new List<string>();
        foreach (var l in c.AllLessons())
            for (int i = 0; i < l.Exercises.Count; i++)
                foreach (char ch in l.Exercises[i].Distinct())
                    if (!layout.Contains(ch))
                        problems.Add($"{l.Id}[{i + 1}]: символ «{ch}» (U+{(int)ch:X4}) нельзя набрать на раскладке {layout.Id}");
        return problems;
    }
}
