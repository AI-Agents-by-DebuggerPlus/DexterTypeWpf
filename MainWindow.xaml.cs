using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DexterType;

public partial class MainWindow : Window
{
    static Color C(string key) => (Color)Application.Current.Resources[key];

    static Brush B(string s) => (Brush)new BrushConverter().ConvertFromString(s)!;

    readonly TrainerEngine _e = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly Dictionary<char, Border> _keys = new();
    readonly List<HistoryEntry> _history = HistoryStore.Load();
    Border? _next;
    public readonly MediaPlayer _musicPlayer = new();

    public Layout CurrentLayout => (Layout)LayoutBox.SelectedItem;

    public MainWindow()
    {
        InitializeComponent();
        try { 
            IconGenerator.Generate("icon.png");
            this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath("icon.png")));
        } catch {}
        LayoutBox.ItemsSource = Layouts.All;
        LayoutBox.DisplayMemberPath = "Name";
        LayoutBox.SelectionChanged += (_, _) => OnLayout();
        LessonBox.SelectionChanged += (_, _) => Restart();
        _timer.Tick += (_, _) => UpdateStats();
        LayoutBox.SelectedIndex = 0;
        ShowHistory();
        this.Loaded += (s, e) => UpdateTextPosition();
        
        // Load music preference
        string musicPath = System.IO.File.Exists("music.txt") ? System.IO.File.ReadAllText("music.txt") : "";
        if (!string.IsNullOrEmpty(musicPath)) PlayMusic(musicPath);
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

    void OnSettings(object s, RoutedEventArgs e)
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
    }

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
        TextBorder.Background = B("#FFEBEE");
        await Task.Delay(140);
        TextBorder.Background = Brushes.White;
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
        if (TextView == null || TextCanvas == null) return;
        
        TextView.Inlines.Clear();
        for (int i = 0; i < _e.Text.Length; i++)
        {
            var ch = _e.Text[i] == ' ' ? ' ' : _e.Text[i];
            var run = new Run(ch.ToString())
            {
                Foreground = i < _e.Pos ? B("#2ECC71") : i == _e.Pos ? Brushes.Black : B("#BBB")
            };
            if (i == _e.Pos) 
            {
                run.Background = B("#FFF176");
                run.FontWeight = FontWeights.Bold;
            }
            TextView.Inlines.Add(run);
        }
        Highlight();
        
        UpdateTextPosition();
    }

    private void UpdateTextPosition()
    {
        if (TextCanvas == null || TextCanvas.ActualWidth <= 0) return;
        double charWidth = 19.8; 
        double center = TextCanvas.ActualWidth / 2;
        TextTransform.X = center - (_e.Pos * charWidth) - (charWidth / 2);
    }

    public void PlayMusic(string? path)
    {
        _musicPlayer.Stop();
        if (string.IsNullOrEmpty(path)) return;
        try {
            _musicPlayer.Open(new Uri(path));
            _musicPlayer.MediaEnded += (s, e) => { _musicPlayer.Position = TimeSpan.Zero; _musicPlayer.Play(); };
            _musicPlayer.Play();
            System.IO.File.WriteAllText("music.txt", path);
        } catch {}
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
        Color baseColor = f < 0 ? C("ColorThumb") : f switch {
            0 or 7 => C("ColorPinky"),
            1 or 6 => C("ColorRing"),
            2 or 5 => C("ColorMiddle"),
            3 or 4 => C("ColorIndex"),
            _ => C("ColorThumb")
        };
        
        if (on) {
            b.Background = new SolidColorBrush(baseColor);
            b.BorderBrush = B("#2C3E50");
            b.BorderThickness = new Thickness(2);
        } else {
            b.Background = new SolidColorBrush(Color.FromArgb(64, baseColor.R, baseColor.G, baseColor.B)); 
            b.BorderBrush = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
        }
        ((TextBlock)b.Child).Foreground = (Brush)Application.Current.Resources["ThemeMainText"];
    }

    public void BuildKeyboard(Layout l)
    {
        Kb.Children.Clear(); _keys.Clear();
        double[] offset = { 0, 25, 60 };
        for (int r = 0; r < l.Rows.Length; r++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, 
                                     HorizontalAlignment = HorizontalAlignment.Center,
                                     Margin = new Thickness(offset[r], 0, 0, 8) };
            for (int i = 0; i < l.Rows[r].Length; i++)
            {
                int finger = Layouts.Fingers[i];
                row.Children.Add(MakeKey(char.ToUpper(l.Rows[r][i]).ToString(), l.Rows[r][i], finger, 50));
            }
            Kb.Children.Add(row);
        }
        var space = new StackPanel { Orientation = Orientation.Horizontal, 
                                   HorizontalAlignment = HorizontalAlignment.Center,
                                   Margin = new Thickness(0, 8, 0, 0) };
        space.Children.Add(MakeKey("", ' ', -1, 300));
        Kb.Children.Add(space);
    }

    Border MakeKey(string label, char c, int finger, double width)
    {
        var b = new Border
        {
            Width = width, Height = 50, Margin = new Thickness(4), Tag = finger,
            CornerRadius = new CornerRadius(12),
            Child = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold,
                FontSize = 18,
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
