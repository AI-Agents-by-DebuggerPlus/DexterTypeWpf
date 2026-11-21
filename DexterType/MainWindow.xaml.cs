using System.IO;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dexter.Core.Audio;
using Dexter.Core.Courses;
using Dexter.Core.Keyboard;
using Dexter.Core.Storage;
using Dexter.Core.Typing;
using DexterType.Ui;

namespace DexterType;

public partial class MainWindow : Window
{
    sealed record RowParts(Border Row, TextBlock Status, TextBlock Best);

    readonly List<Course> _courses = new();
    readonly Dictionary<Course, RadioButton> _tabs = new();
    readonly Dictionary<Lesson, RowParts> _rows = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    Course _course = null!;
    List<Lesson> _all = new();
    Lesson _lesson = null!;
    KeyboardLayoutDef _layout = KeyboardLayouts.Ru;
    TypingSession? _session;
    double _charWidth = 22;
    bool _flash, _resultShown, _building;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Create();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        Title = $"DexterType {v?.Major}.{v?.Minor}";
        _timer.Tick += (_, _) => UpdateStats();
        Loaded += OnLoaded;
        Closing += (_, _) =>
        {
            App.Settings.LastCourse = _course?.Id ?? "";
            App.Settings.LastLesson = _lesson?.Id ?? "";
            SettingsStore.Save(App.Settings);
        };
    }

    // ------------------------------------------------------------------ запуск и навигация

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        MeasureChar();

        var load = CourseLoader.LoadDirectories(
            Path.Combine(AppContext.BaseDirectory, "Courses"),
            Path.Combine(AppPaths.DataDir, "courses"));
        _courses.AddRange(load.Courses);

        if (_courses.Count == 0)
        {
            MessageBox.Show("Не найдено ни одного курса в папке Courses.\n" + string.Join("\n", load.Errors),
                "DexterType", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }
        if (load.Errors.Count > 0)
            MessageBox.Show("Часть курсов не загружена:\n" + string.Join("\n", load.Errors),
                "DexterType", MessageBoxButton.OK, MessageBoxImage.Warning);

        BuildCourseTabs();
        var start = _courses.FirstOrDefault(c => c.Id == App.Settings.LastCourse) ?? _courses[0];
        SelectCourse(start, App.Settings.LastLesson);
        ApplyViewSettings();
        Keyboard.Focus(this);
    }

    void MeasureChar()
    {
        var typeface = new Typeface(TextView.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface,
            TextView.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        _charWidth = ft.WidthIncludingTrailingWhitespace;
    }

    void BuildCourseTabs()
    {
        CoursePanel.Children.Clear();
        _tabs.Clear();
        foreach (var course in _courses)
        {
            var rb = new RadioButton
            {
                Content = course.Title, GroupName = "courses",
                Style = (Style)FindResource("TabRadio"),
            };
            rb.Checked += (_, _) => { if (!_building && course != _course) SelectCourse(course); };
            _tabs[course] = rb;
            CoursePanel.Children.Add(rb);
        }
    }

    void SelectCourse(Course course, string? lessonId = null)
    {
        _building = true;
        _course = course;
        _all = course.AllLessons().ToList();
        _layout = KeyboardLayouts.ById(course.Layout);
        foreach (var (c, rb) in _tabs) rb.IsChecked = c == course;
        _building = false;

        Kb.SetLayout(_layout);
        BuildLessonList();

        var lesson = _all.FirstOrDefault(l => l.Id == lessonId && IsUnlocked(l))
                     ?? _all.FirstOrDefault(l => IsUnlocked(l) && !App.Progress.IsPassed(course.Id, l.Id))
                     ?? _all[0];
        SelectLesson(lesson);
    }

    void SelectLesson(Lesson lesson)
    {
        if (!IsUnlocked(lesson))
        {
            Hint.Text = "Урок закрыт: сначала зачтите предыдущий (или отключите последовательное прохождение в настройках).";
            return;
        }
        _lesson = lesson;
        App.Settings.LastCourse = _course.Id;
        App.Settings.LastLesson = lesson.Id;
        IntroText.Text = lesson.Intro;
        HighlightRow();
        Restart();
    }

    bool IsUnlocked(Lesson lesson)
    {
        if (!App.Settings.SequentialUnlock) return true;
        int i = _all.IndexOf(lesson);
        return i <= 0 || App.Progress.IsPassed(_course.Id, _all[i - 1].Id);
    }

    void GoNext()
    {
        int i = _all.IndexOf(_lesson);
        if (i + 1 >= _all.Count) { Hint.Text = "Это последний урок курса. Поздравляем!"; return; }
        var next = _all[i + 1];
        if (!IsUnlocked(next)) { Hint.Text = "Сначала зачтите текущий урок."; return; }
        SelectLesson(next);
    }

    void GoPrev()
    {
        int i = _all.IndexOf(_lesson);
        if (i > 0) SelectLesson(_all[i - 1]);
    }

    // ------------------------------------------------------------------ список уроков

    void BuildLessonList()
    {
        LessonList.Children.Clear();
        _rows.Clear();
        int n = 0;
        foreach (var part in _course.Parts)
        {
            var header = new TextBlock
            {
                Text = part.Title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 14, 0, 4),
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "ThemeSubText");
            LessonList.Children.Add(header);

            foreach (var lesson in part.Lessons)
            {
                n++;
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var status = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
                var title = new TextBlock
                {
                    Text = $"{n}. {lesson.Title}", TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var best = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                best.SetResourceReference(TextBlock.ForegroundProperty, "ThemeSubText");
                Grid.SetColumn(status, 0); Grid.SetColumn(title, 1); Grid.SetColumn(best, 2);
                grid.Children.Add(status); grid.Children.Add(title); grid.Children.Add(best);

                var row = new Border
                {
                    Child = grid, CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 5, 8, 5),
                    Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent,
                    ToolTip = lesson.Intro,
                };
                var captured = lesson;
                row.MouseLeftButtonUp += (_, _) => { SelectLesson(captured); Keyboard.Focus(this); };
                LessonList.Children.Add(row);
                _rows[lesson] = new RowParts(row, status, best);
            }
        }
        UpdateRowStatuses();
    }

    void UpdateRowStatuses()
    {
        foreach (var (lesson, parts) in _rows)
        {
            var progress = App.Progress.Get(_course.Id, lesson.Id);
            bool locked = !IsUnlocked(lesson);
            bool passed = progress is { Passed: true };

            parts.Status.Text = locked ? "🔒" : passed ? "✓" : progress is null ? "○" : "◐";
            parts.Status.SetResourceReference(TextBlock.ForegroundProperty, passed ? "ThemeGood" : "ThemeSubText");
            parts.Best.Text = progress is null ? "" : progress.BestCpm.ToString();
            parts.Row.Opacity = locked ? 0.5 : 1.0;
        }
    }

    void HighlightRow()
    {
        foreach (var (lesson, parts) in _rows)
        {
            if (lesson == _lesson) parts.Row.SetResourceReference(Border.BackgroundProperty, "ThemeSelected");
            else parts.Row.Background = Brushes.Transparent;
        }
        if (_rows.TryGetValue(_lesson, out var current)) current.Row.BringIntoView();
    }

    // ------------------------------------------------------------------ прохождение урока

    void Restart()
    {
        _timer.Stop();
        HideResult();
        _flash = false;

        var criteria = _course.CriteriaFor(_lesson);
        _session = new TypingSession(_lesson, criteria, _layout);
        CpmCaption.Text = $"зн/мин · норма ≥ {criteria.MinCpm}";
        AccCaption.Text = $"точность · норма ≥ {criteria.MinAccuracy}%";

        Render();
        UpdateStats();
        UpdateStatus();
        Keyboard.Focus(this);
    }

    void OnText(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
        if (_session is null || _resultShown || string.IsNullOrEmpty(e.Text)) return;

        foreach (char c in e.Text)
        {
            int exerciseBefore = _session.ExerciseIndex;
            var outcome = _session.Press(c);

            switch (outcome)
            {
                case KeyOutcome.Ignored:
                    break;

                case KeyOutcome.Correct:
                    if (!_timer.IsEnabled && !_session.Finished) _timer.Start();
                    Render();
                    if (_session.Finished) { Finish(); return; }
                    if (_session.ExerciseIndex != exerciseBefore) App.Sound.Play(SoundKind.ExerciseDone);
                    break;

                default:
                    if (!_timer.IsEnabled) _timer.Start();
                    OnMistake(outcome);
                    break;
            }
        }
        UpdateStats();
    }

    void OnMistake(KeyOutcome outcome)
    {
        App.Sound.Play(outcome switch
        {
            KeyOutcome.CaseMismatch => SoundKind.CaseMismatch,
            KeyOutcome.WrongLayout => SoundKind.WrongLayout,
            _ => SoundKind.Wrong,
        });

        // Понятная подсказка вместо молчаливого «ошибка»
        if (outcome == KeyOutcome.WrongLayout)
            Hint.Text = $"Переключите раскладку на «{_layout.Name}» (Alt+Shift или Win+Пробел)";
        else if (outcome == KeyOutcome.CaseMismatch)
            Hint.Text = Keyboard.IsKeyToggled(Key.CapsLock)
                ? "Включён Caps Lock — выключите его"
                : "Не тот регистр: проверьте Shift";

        FlashError();
    }

    async void FlashError()
    {
        _flash = true;
        Render(keepHint: true);
        await Task.Delay(170);
        _flash = false;
        if (_session is { Finished: false }) Render(keepHint: true);
    }

    void Finish()
    {
        _timer.Stop();
        UpdateStats();
        var s = _session!;
        App.Progress.Record(_course.Id, s, DateTime.Now);
        App.Sound.Play(s.Passed ? SoundKind.LessonPassed : SoundKind.LessonFailed);
        UpdateRowStatuses();
        ShowResult(s);
    }

    // ------------------------------------------------------------------ отрисовка

    void Render(bool keepHint = false)
    {
        if (_session is null) return;
        var s = _session;

        var good = (Brush)FindResource("ThemeGood");
        var sub = (Brush)FindResource("ThemeSubText");
        var text = (Brush)FindResource("ThemeText");
        var bad = (Brush)FindResource("ThemeBad");
        var caret = (Brush)FindResource("ThemeCaret");

        TextView.Inlines.Clear();
        for (int i = 0; i < s.Text.Length; i++)
        {
            var run = new Run(s.Text[i].ToString());
            if (i < s.Pos) run.Foreground = good;
            else if (i == s.Pos)
            {
                run.Foreground = _flash ? Brushes.White : text;
                run.Background = _flash ? bad : caret;
            }
            else run.Foreground = sub;
            TextView.Inlines.Add(run);
        }

        PositionText();
        ExerciseLabel.Text = $"Упражнение {s.ExerciseIndex + 1} из {s.ExerciseCount}";
        LessonProgress.Value = s.ProgressFraction * 100;
        Kb.Highlight(s.Expected);
        if (!keepHint && s.Expected is char c) Hint.Text = HintFor(c);
    }

    void PositionText()
    {
        if (_session is null || TextCanvas.ActualWidth <= 0) return;
        TextTransform.X = TextCanvas.ActualWidth / 2 - _session.Pos * _charWidth - _charWidth / 2;
        double height = TextView.ActualHeight > 0 ? TextView.ActualHeight : TextView.FontSize * 1.4;
        Canvas.SetTop(TextView, Math.Max(0, (TextCanvas.ActualHeight - height) / 2));
    }

    void OnTextSizeChanged(object sender, SizeChangedEventArgs e) => PositionText();

    string HintFor(char c)
    {
        var hit = _layout.Find(c);
        string name = c == ' ' ? "пробел" : c.ToString();
        if (hit is null) return $"Далее: «{name}»";

        string finger = Fingers.Name(hit.Key.Finger);
        return hit.NeedsShift && hit.ShiftKey is not null
            ? $"Далее: «{name}» — Shift ({Fingers.Name(hit.ShiftKey.Finger)}) + {finger}"
            : $"Далее: «{name}» — {finger}";
    }

    void UpdateStats()
    {
        if (_session is null) return;
        var s = _session;
        var c = s.Criteria;

        CpmText.Text = s.Cpm.ToString();
        AccText.Text = s.Accuracy + "%";
        ErrText.Text = s.Errors.ToString();
        TimeText.Text = TimeSpan.FromSeconds(s.Seconds).ToString(@"m\:ss");

        CpmText.SetResourceReference(TextBlock.ForegroundProperty, s.Cpm >= c.MinCpm ? "ThemeGood" : "ThemeText");
        AccText.SetResourceReference(TextBlock.ForegroundProperty,
            s.Accuracy >= c.MinAccuracy ? "ThemeGood" : s.Errors > 0 ? "ThemeBad" : "ThemeText");
        ErrText.SetResourceReference(TextBlock.ForegroundProperty, s.Errors > 0 ? "ThemeBad" : "ThemeText");
    }

    void UpdateStatus()
    {
        var c = _course.CriteriaFor(_lesson);
        int index = _all.IndexOf(_lesson) + 1;
        StatusText.Text = $"{_course.Title}  ·  Урок {index} из {_all.Count}: {_lesson.Title}  ·  " +
                          $"Норма: ≥ {c.MinCpm} зн/мин, ≥ {c.MinAccuracy}%  ·  " +
                          "Esc — заново, Ctrl+←/→ — уроки, F2 — настройки, F7 — статистика";
    }

    void ApplyViewSettings() => Kb.Visibility = App.Settings.ShowKeyboard ? Visibility.Visible : Visibility.Collapsed;

    // ------------------------------------------------------------------ итог урока

    void ShowResult(TypingSession s)
    {
        _resultShown = true;
        bool passed = s.Passed;
        var c = s.Criteria;

        ResultTitle.Text = passed ? "Урок зачтён!" : "Пока не зачтено";
        ResultTitle.SetResourceReference(TextBlock.ForegroundProperty, passed ? "ThemeGood" : "ThemeBad");

        var lines = new List<string>
        {
            $"Скорость: {s.Cpm} зн/мин (нужно ≥ {c.MinCpm})",
            $"Точность: {s.Accuracy}% (нужно ≥ {c.MinAccuracy}%)",
            $"Ошибок: {s.Errors} · время {TimeSpan.FromSeconds(s.Seconds):m\\:ss}",
        };
        var worst = s.Mistakes.OrderByDescending(kv => kv.Value).Take(4).ToList();
        if (worst.Count > 0)
            lines.Add("Чаще всего ошибки на: " + string.Join(", ", worst.Select(kv => $"«{(kv.Key == ' ' ? "пробел" : kv.Key.ToString())}» ×{kv.Value}")));
        ResultDetail.Text = string.Join("\n", lines);

        ResultNext.Content = passed ? "Следующий урок ▶ (Enter)" : "Повторить (Enter)";
        ResultRepeat.Visibility = passed ? Visibility.Visible : Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Visible;
        Hint.Text = passed ? "Enter — следующий урок, Esc — повторить" : "Enter или Esc — повторить урок";
    }

    void HideResult()
    {
        _resultShown = false;
        ResultCard.Visibility = Visibility.Collapsed;
    }

    // ------------------------------------------------------------------ команды

    void OnKey(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        switch (e.Key)
        {
            case Key.Escape:
                Restart(); e.Handled = true; break;
            case Key.Enter when _resultShown:
                OnResultNext(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.Left when ctrl:
                GoPrev(); e.Handled = true; break;
            case Key.Right when ctrl:
                GoNext(); e.Handled = true; break;
            case Key.F2:
                OnSettings(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.F7:
                OnStats(this, new RoutedEventArgs()); e.Handled = true; break;
        }
    }

    void OnPrev(object sender, RoutedEventArgs e) { GoPrev(); Keyboard.Focus(this); }
    void OnNext(object sender, RoutedEventArgs e) { GoNext(); Keyboard.Focus(this); }
    void OnRepeat(object sender, RoutedEventArgs e) => Restart();

    void OnResultNext(object sender, RoutedEventArgs e)
    {
        if (_session?.Passed == true) GoNext();
        else Restart();
    }

    void OnStats(object sender, RoutedEventArgs e)
    {
        new StatsWindow(_course) { Owner = this }.ShowDialog();
        Keyboard.Focus(this);
    }

    void OnSettings(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(_course) { Owner = this }.ShowDialog();
        ApplyViewSettings();
        Kb.Repaint();
        UpdateRowStatuses();
        HighlightRow();
        Render();
        Keyboard.Focus(this);
    }
}
