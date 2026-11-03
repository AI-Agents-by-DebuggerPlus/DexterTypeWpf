using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DexterType;

public partial class MainWindow : Window
{
    static readonly string[] Strong = { "#E0405A", "#E67E22", "#D4A017", "#2EAA5E", "#18A5B8", "#3B6DF0", "#7E57C2", "#C2409E" };
    static Brush B(string s) => (Brush)new BrushConverter().ConvertFromString(s)!;

    readonly TrainerEngine _e = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly Dictionary<char, Border> _keys = new();
    readonly List<HistoryEntry> _history = HistoryStore.Load();
    Border? _next;

    public MainWindow()
    {
        InitializeComponent();
        LayoutBox.ItemsSource = Layouts.All;
        LayoutBox.DisplayMemberPath = "Name";
        LayoutBox.SelectionChanged += (_, _) => OnLayout();
        LessonBox.SelectionChanged += (_, _) => Restart();
        _timer.Tick += (_, _) => UpdateStats();
        LayoutBox.SelectedIndex = 0;
        ShowHistory();
    }

    void OnLayout()
    {
        var l = (Layout)LayoutBox.SelectedItem;
        BuildKeyboard(l);
        LessonBox.ItemsSource = l.Groups.Select((g, i) => $"Урок {i + 1}: {g}").Append("Слова").ToList();
        LessonBox.SelectedIndex = 0;
    }

    void Restart()
    {
        if (LayoutBox.SelectedItem is not Layout l || LessonBox.SelectedIndex < 0) return;
        _timer.Stop();
        _e.Start(l, LessonBox.SelectedIndex);
        Render(); UpdateStats();
        Focus(); // чтобы ввод шёл в окно, а не в ComboBox
    }

    void OnRestart(object s, RoutedEventArgs e) => Restart();

    void OnKey(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.Enter && _e.Done)) { Restart(); e.Handled = true; }
    }

    void OnText(object s, TextCompositionEventArgs e)
    {
        e.Handled = true;
        if (_e.Done || e.Text.Length == 0 || char.IsControl(e.Text[0])) return;
        if (!_timer.IsEnabled) _timer.Start();
        if (_e.Press(e.Text[0])) { Render(); if (_e.Done) Finish(); }
        else Flash();
        UpdateStats();
    }

    async void Flash()
    {
        TextBorder.BorderBrush = Brushes.IndianRed;
        await Task.Delay(140);
        TextBorder.BorderBrush = B("#DDE1E8");
    }

    void Finish()
    {
        _timer.Stop(); UpdateStats();
        _history.Add(new(DateTime.Now, _e.Current.Name, _e.Lesson, _e.Cpm, _e.Accuracy, _e.Errors));
        HistoryStore.Save(_history); ShowHistory();
        Hint.Text = _e.Accuracy >= 97 && _e.Cpm >= 150
            ? "Отлично! Критерий выполнен (от 97% точности и от 150 зн/мин). Enter: ещё раз."
            : "Готово. Для зачёта нужно от 97% точности и от 150 зн/мин. Enter: ещё раз.";
    }

    void Render()
    {
        TextView.Inlines.Clear();
        for (int i = 0; i < _e.Text.Length; i++)
        {
            var ch = _e.Text[i] == ' ' ? '·' : _e.Text[i];
            var run = new Run(ch.ToString())
            {
                Foreground = i < _e.Pos ? Brushes.SeaGreen : i == _e.Pos ? Brushes.White : Brushes.Gray
            };
            if (i == _e.Pos) run.Background = B("#3B6DF0");
            TextView.Inlines.Add(run);
        }
        Highlight();
    }

    void Highlight()
    {
        if (_next != null) { Paint(_next, false); _next = null; }
        if (_e.Pos < _e.Text.Length && _keys.TryGetValue(_e.Text[_e.Pos], out var b))
        {
            _next = b; Paint(b, true);
            int f = (int)b.Tag;
            var c = _e.Text[_e.Pos];
            Hint.Text = $"Далее: «{(c == ' ' ? "пробел" : c.ToString())}», {(f < 0 ? "большой палец" : Layouts.FingerNames[f])}";
        }
    }

    static void Paint(Border b, bool on)
    {
        int f = (int)b.Tag;
        b.Background = B(on ? (f < 0 ? "#808080" : Strong[f]) : (f < 0 ? "#14808080" : "#2B" + Strong[f][1..]));
        ((TextBlock)b.Child).Foreground = on ? Brushes.White : Brushes.Black;
    }

    void BuildKeyboard(Layout l)
    {
        Kb.Children.Clear(); _keys.Clear();
        double[] offset = { 0, 22, 50 };
        for (int r = 0; r < l.Rows.Length; r++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(offset[r], 0, 0, 4) };
            for (int i = 0; i < l.Rows[r].Length; i++)
                row.Children.Add(MakeKey(char.ToUpper(l.Rows[r][i]).ToString(), l.Rows[r][i], Layouts.Fingers[i], 44));
            Kb.Children.Add(row);
        }
        var space = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(100, 0, 0, 0) };
        space.Children.Add(MakeKey("пробел", ' ', -1, 260));
        Kb.Children.Add(space);
    }

    Border MakeKey(string label, char c, int finger, double width)
    {
        var b = new Border
        {
            Width = width, Height = 44, Margin = new Thickness(0, 0, 4, 0), Tag = finger,
            CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = B("#DDE1E8"),
            Child = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Paint(b, false);
        _keys[c] = b;
        return b;
    }

    void UpdateStats()
    {
        CpmText.Text = _e.Cpm.ToString();
        AccText.Text = _e.Accuracy + "%";
        ErrText.Text = _e.Errors.ToString();
        int s = (int)_e.Seconds;
        TimeText.Text = $"{s / 60}:{s % 60:00}";
    }

    void ShowHistory() => HistoryList.ItemsSource = _history.TakeLast(8).Reverse().Select(h =>
        $"{h.Cpm} зн/мин · {h.Accuracy}% · ошибок {h.Errors} · {h.Layout}, " +
        $"{(h.Lesson < Layouts.All.First(x => x.Name == h.Layout).Groups.Length ? "урок " + (h.Lesson + 1) : "слова")} · {h.When:dd.MM HH:mm}").ToList();
}
