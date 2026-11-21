using Dexter.Core.Audio;
using Dexter.Core.Courses;
using Dexter.Core.Keyboard;
using Dexter.Core.Storage;
using Dexter.Core.Typing;

int failures = 0;
void Check(bool ok, string what)
{
    if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); }
}

// --- Папка с курсами ---------------------------------------------------------------------------------------
string? FindCourses()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var p = Path.Combine(dir.FullName, "DexterType", "Courses");
        if (Directory.Exists(p)) return p;
        dir = dir.Parent;
    }
    return null;
}
var coursesDir = args.Length > 0 ? args[0] : FindCourses();
if (coursesDir is null) { Console.WriteLine("Папка DexterType/Courses не найдена."); return 2; }

// --- 1. Раскладки ------------------------------------------------------------------------------------------
Console.WriteLine("[1] Раскладки");
var ru = KeyboardLayouts.Ru; var en = KeyboardLayouts.En;
var a = ru.Find('а'); var bigA = ru.Find('А'); var comma = ru.Find(','); var qmark = ru.Find('?');
Check(a is { NeedsShift: false } && a.Key.Finger == 3, "«а» — левый указательный, без Shift");
Check(bigA is { NeedsShift: true } && bigA.ShiftKey?.Finger == 7, "«А» — правый Shift (мизинец)");
Check(ru.Find('О') is { NeedsShift: true } && ru.Find('О')!.ShiftKey?.Finger == 0, "«О» — левый Shift");
Check(comma is { NeedsShift: true } && comma.Key.Finger == 7, "«,» в русской — Shift + точка");
Check(qmark is { NeedsShift: true }, "«?» в русской — с Shift");
Check(ru.Find(' ') is { Key.Kind: KeyKind.Space }, "пробел найден");
Check(en.Find('f')!.Key.Finger == 3 && en.Find('j')!.Key.Finger == 4, "F/J — указательные");
Check(en.Find(':') is { NeedsShift: true }, "«:» — с Shift");
foreach (var l in KeyboardLayouts.All)
    foreach (var row in l.Rows)
        Check(Math.Abs(row.Sum(k => k.Width) - (row.Any(k => k.Kind == KeyKind.Space) ? 10.0 : 15.0)) < 0.001,
              $"ширина ряда {l.Id}: {row.Sum(k => k.Width)}");

// --- 2. Курсы ----------------------------------------------------------------------------------------------
Console.WriteLine("[2] Курсы из " + coursesDir);
var loaded = CourseLoader.LoadDirectories(coursesDir);
foreach (var e in loaded.Errors) { failures++; Console.WriteLine("  FAIL: " + e); }
Check(loaded.Courses.Count >= 2, "загружено минимум 2 курса");

foreach (var course in loaded.Courses)
{
    var lessons = course.AllLessons().ToList();
    Console.WriteLine($"  {course.Id}: {lessons.Count} уроков, {lessons.Sum(l => l.Exercises.Count)} упражнений");
    foreach (var p in CourseLoader.CheckTypeable(course)) Check(false, p);

    var layout = KeyboardLayouts.ById(course.Layout);

    // Идеальное прохождение каждого урока на скорости 6 зн/сек (360 зн/мин) должно зачитываться.
    foreach (var lesson in lessons)
    {
        var t = new DateTime(2026, 1, 1);
        var s = new TypingSession(lesson, course.CriteriaFor(lesson), layout, () => t);
        foreach (var ex in lesson.Exercises)
            foreach (char c in ex)
            {
                t = t.AddMilliseconds(1000.0 / 6);
                var r = s.Press(c);
                if (r != KeyOutcome.Correct) { Check(false, $"{lesson.Id}: {c} → {r}"); break; }
            }
        Check(s.Finished && s.Passed && s.Accuracy == 100, $"{lesson.Id}: идеальный проход зачтён (cpm={s.Cpm}, acc={s.Accuracy})");
        Check(Math.Abs(s.ProgressFraction - 1.0) < 1e-9, $"{lesson.Id}: прогресс = 100%");
    }
}

// --- 3. Движок: ошибки и критерии ----------------------------------------------------------------------------
Console.WriteLine("[3] Движок");
{
    var lesson = new Lesson { Id = "t1", Exercises = { "Аб", "в" } };
    var t = new DateTime(2026, 1, 1);
    var s = new TypingSession(lesson, new Criteria { MinCpm = 10, MinAccuracy = 90 }, ru, () => t);
    Check(s.Press('\r') == KeyOutcome.Ignored, "управляющий символ игнорируется");
    Check(!s.Started, "игнор не запускает таймер");
    Check(s.Press('а') == KeyOutcome.CaseMismatch, "«а» вместо «А» — ошибка регистра");
    Check(s.Press('b') == KeyOutcome.WrongLayout, "латиница в русском уроке — не та раскладка");
    Check(s.Press('ы') == KeyOutcome.WrongKey, "«ы» вместо «А» — неверная клавиша");
    Check(s.Pos == 0 && s.Errors == 3, "ошибки не двигают курсор");
    Check(s.Press('А') == KeyOutcome.Correct, "«А» принята");
    Check(s.Press('б') == KeyOutcome.Correct && s.ExerciseIndex == 1 && s.Pos == 0, "переход ко второму упражнению");
    t = t.AddSeconds(10);
    Check(s.Press('в') == KeyOutcome.Correct && s.Finished, "урок завершён");
    Check(s.Press('в') == KeyOutcome.Ignored, "после финиша ввод игнорируется");
    Check(s.Accuracy == 50, $"точность 3/6 = 50% (получено {s.Accuracy})");
    Check(!s.Passed, "50% точности — не зачёт");
    Check(s.Mistakes['А'] == 3, "ошибки учтены по ожидаемому символу");
}
{
    var lesson = new Lesson { Id = "t2", Exercises = { "ab" } };
    var t = new DateTime(2026, 1, 1);
    var s = new TypingSession(lesson, new Criteria { MinCpm = 1000, MinAccuracy = 90 }, en, () => t);
    s.Press('a'); t = t.AddSeconds(2); s.Press('b');
    Check(s.Finished && s.Cpm == 60 && !s.Passed, $"медленно — не зачёт (cpm={s.Cpm})");
}

// --- 4. Звуки ----------------------------------------------------------------------------------------------
Console.WriteLine("[4] Звуки");
foreach (var pack in SoundSynth.Packs)
    foreach (var kind in Enum.GetValues<SoundKind>())
    {
        var wav = SoundSynth.Render(pack, kind, 0.6);
        bool header = wav.Length > 44 && wav[0] == 'R' && wav[1] == 'I' && wav[8] == 'W';
        int dataLen = BitConverter.ToInt32(wav, 40);
        Check(header && dataLen == wav.Length - 44, $"{pack}/{kind}: корректный WAV ({wav.Length} байт)");
        short peak = 0;
        for (int i = 44; i + 1 < wav.Length; i += 2) peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(wav, i)));
        Check(peak > 500, $"{pack}/{kind}: звук не пустой (пик {peak})");
    }
Check(SoundSynth.Render("classic", SoundKind.Wrong, 0).All(b => true), "громкость 0 не падает");

// --- 5. Хранилище ------------------------------------------------------------------------------------------
Console.WriteLine("[5] Хранилище");
{
    var tmp = Path.Combine(Path.GetTempPath(), "dexter-selfcheck-" + Guid.NewGuid().ToString("N"));
    AppPaths.DataDir = tmp;
    var course = loaded.Courses[0];
    var lesson = course.AllLessons().First();
    var layout = KeyboardLayouts.ById(course.Layout);
    var t = new DateTime(2026, 1, 1);
    var s = new TypingSession(lesson, course.CriteriaFor(lesson), layout, () => t);
    s.Press('?'); // ошибка
    foreach (var ex in lesson.Exercises) foreach (char c in ex) { t = t.AddMilliseconds(150); s.Press(c); }

    var store = new ProgressStore();
    var rec = store.Record(course.Id, s, DateTime.Now);
    Check(store.Get(course.Id, lesson.Id)?.Attempts == 1, "попытка записана");
    Check(rec.Mistakes.Count == 1, "ошибка попала в журнал");

    var store2 = new ProgressStore(); // перечитываем с диска
    Check(store2.Get(course.Id, lesson.Id)?.Attempts == 1, "прогресс переживает перезапуск");
    Check(store2.TopMistakes(course.Id, 5).Count == 1, "топ ошибок считается");
    store2.ResetCourse(course.Id);
    Check(store2.Get(course.Id, lesson.Id) is null && store2.Attempts(course.Id).Count == 0, "сброс курса");

    var settings = new AppSettings { Theme = "Dark", SoundVolume = 0.25 };
    SettingsStore.Save(settings);
    var back = SettingsStore.Load();
    Check(back.Theme == "Dark" && Math.Abs(back.SoundVolume - 0.25) < 1e-9, "настройки сохраняются");

    // Повреждённый файл не должен ронять загрузку
    File.WriteAllText(AppPaths.File("progress.json"), "{ это не json");
    var broken = new ProgressStore();
    Check(broken.Attempts().Count == 0, "повреждённый progress.json → пустой прогресс");
    try { Directory.Delete(tmp, true); } catch { }
}

Console.WriteLine(failures == 0 ? "\nВсе проверки пройдены." : $"\nПровалено проверок: {failures}");
return failures == 0 ? 0 : 1;
