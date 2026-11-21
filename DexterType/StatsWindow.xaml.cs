using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Dexter.Core.Courses;

namespace DexterType;

public partial class StatsWindow : Window
{
    const int ChartAttempts = 30;
    readonly Course _course;

    public StatsWindow(Course course)
    {
        _course = course;
        InitializeComponent();
        FillSummary();
        FillLessons();
        FillMistakes();
        Loaded += (_, _) => DrawChart();
    }

    void FillSummary()
    {
        var lessons = _course.AllLessons().ToList();
        int passed = lessons.Count(l => App.Progress.IsPassed(_course.Id, l.Id));
        var attempts = App.Progress.Attempts(_course.Id);
        var last = attempts.TakeLast(10).ToList();

        string avg = last.Count == 0 ? "—" : $"{last.Average(a => a.Cpm):0} зн/мин, {last.Average(a => a.Accuracy):0}%";
        string best = attempts.Count == 0 ? "—" : $"{attempts.Max(a => a.Cpm)} зн/мин";
        SummaryText.Text = $"{_course.Title}: зачтено {passed} из {lessons.Count} уроков · попыток {attempts.Count} · " +
                           $"в среднем за последние 10: {avg} · рекорд {best}";
    }

    void FillLessons()
    {
        int n = 0;
        foreach (var lesson in _course.AllLessons())
        {
            n++;
            var p = App.Progress.Get(_course.Id, lesson.Id);
            string mark = p is { Passed: true } ? "✓" : p is null ? "○" : "◐";

            var left = new TextBlock { Text = $"{mark}  {n}. {lesson.Title}", TextTrimming = TextTrimming.CharacterEllipsis };
            left.SetResourceReference(TextBlock.ForegroundProperty, p is { Passed: true } ? "ThemeGood" : "ThemeText");

            var right = new TextBlock
            {
                Text = p is null ? "" : $"{p.BestCpm} зн/мин · {p.BestAccuracy}% · ×{p.Attempts}",
                FontSize = 12, Margin = new Thickness(12, 0, 0, 0),
            };
            right.SetResourceReference(TextBlock.ForegroundProperty, "ThemeSubText");

            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);
            row.Children.Add(left);
            LessonsPanel.Children.Add(row);
        }
    }

    void FillMistakes()
    {
        var top = App.Progress.TopMistakes(_course.Id, 12);
        if (top.Count == 0)
        {
            var none = new TextBlock { Text = "Ошибок пока нет.", TextWrapping = TextWrapping.Wrap };
            none.SetResourceReference(TextBlock.ForegroundProperty, "ThemeSubText");
            MistakesPanel.Children.Add(none);
            return;
        }

        int max = top.Max(t => t.Count);
        foreach (var (ch, count) in top)
        {
            var symbol = new TextBlock
            {
                Text = ch == " " ? "␣" : ch, FontFamily = new FontFamily("Consolas"), FontSize = 18,
                FontWeight = FontWeights.SemiBold, Width = 32,
            };
            var number = new TextBlock { Text = count.ToString(), Width = 34, TextAlignment = TextAlignment.Right };
            var bar = new Border
            {
                Height = 8, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(4, 110.0 * count / max), Margin = new Thickness(6, 0, 6, 0),
            };
            bar.SetResourceReference(Border.BackgroundProperty, "ThemeBad");

            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            DockPanel.SetDock(symbol, Dock.Left);
            DockPanel.SetDock(number, Dock.Right);
            row.Children.Add(symbol);
            row.Children.Add(number);
            row.Children.Add(bar);
            MistakesPanel.Children.Add(row);
        }
    }

    void OnChartSizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    /// <summary>График скорости по последним попыткам. Точка зелёная, если попытка зачтена, красная — если нет.</summary>
    void DrawChart()
    {
        Chart.Children.Clear();
        var attempts = App.Progress.Attempts(_course.Id).TakeLast(ChartAttempts).ToList();
        ChartEmpty.Visibility = attempts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (attempts.Count == 0 || Chart.ActualWidth < 80 || Chart.ActualHeight < 60) return;

        const double left = 44, bottom = 22, top = 8, right = 8;
        double w = Chart.ActualWidth - left - right, h = Chart.ActualHeight - top - bottom;
        int maxCpm = Math.Max(60, (int)Math.Ceiling(attempts.Max(a => a.Cpm) / 50.0) * 50);

        var grid = (Brush)FindResource("ThemeBorder");
        var sub = (Brush)FindResource("ThemeSubText");
        var accent = (Brush)FindResource("ThemeAccent");
        var good = (Brush)FindResource("ThemeGood");
        var bad = (Brush)FindResource("ThemeBad");

        for (int i = 0; i <= 4; i++)
        {
            double y = top + h * i / 4;
            Chart.Children.Add(new Line { X1 = left, X2 = left + w, Y1 = y, Y2 = y, Stroke = grid, StrokeThickness = 1 });
            var label = new TextBlock { Text = (maxCpm - maxCpm * i / 4).ToString(), FontSize = 11, Foreground = sub, Width = left - 6, TextAlignment = TextAlignment.Right };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 8);
            Chart.Children.Add(label);
        }

        double Xat(int i) => attempts.Count == 1 ? left + w / 2 : left + w * i / (attempts.Count - 1);
        double Yat(int cpm) => top + h * (1 - (double)cpm / maxCpm);

        var line = new Polyline { Stroke = accent, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
        for (int i = 0; i < attempts.Count; i++) line.Points.Add(new Point(Xat(i), Yat(attempts[i].Cpm)));
        Chart.Children.Add(line);

        for (int i = 0; i < attempts.Count; i++)
        {
            var dot = new Ellipse
            {
                Width = 9, Height = 9, Fill = attempts[i].Passed ? good : bad,
                ToolTip = $"{attempts[i].When:dd.MM HH:mm} · {attempts[i].Cpm} зн/мин · {attempts[i].Accuracy}%",
            };
            Canvas.SetLeft(dot, Xat(i) - 4.5);
            Canvas.SetTop(dot, Yat(attempts[i].Cpm) - 4.5);
            Chart.Children.Add(dot);
        }

        var caption = new TextBlock { Text = $"Последние {attempts.Count} попыток, зн/мин", FontSize = 11, Foreground = sub };
        Canvas.SetLeft(caption, left);
        Canvas.SetTop(caption, Chart.ActualHeight - 18);
        Chart.Children.Add(caption);
    }
}
