using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dexter.Core.Audio;
using Dexter.Core.Courses;
using Dexter.Core.Storage;
using DexterType.Theming;
using Microsoft.Win32;

namespace DexterType;

public partial class SettingsWindow : Window
{
    static readonly (string Id, string Title)[] Packs =
    {
        ("classic", "Классический «бззт»"),
        ("soft", "Мягкий, глухой"),
        ("click", "Щелчок"),
        ("beep", "Короткий писк"),
    };

    static readonly (SoundKind Kind, string Title)[] Tests =
    {
        (SoundKind.Wrong, "Ошибка"),
        (SoundKind.CaseMismatch, "Регистр"),
        (SoundKind.WrongLayout, "Раскладка"),
        (SoundKind.ExerciseDone, "Упражнение"),
        (SoundKind.LessonPassed, "Зачёт"),
        (SoundKind.LessonFailed, "Незачёт"),
    };

    readonly Course _course;
    bool _loading = true;

    public SettingsWindow(Course course)
    {
        _course = course;
        InitializeComponent();

        var s = App.Settings;
        BuildThemes();
        BuildPacks();
        BuildTests();

        SoundCheck.IsChecked = s.SoundEnabled;
        VolumeSlider.Value = Math.Round(s.SoundVolume * 100 / 5) * 5;
        MusicCheck.IsChecked = s.MusicEnabled;
        UpdateMusicName();
        KbCheck.IsChecked = s.ShowKeyboard;
        SeqCheck.IsChecked = s.SequentialUnlock;
        DataPath.Text = "Прогресс и настройки: " + AppPaths.DataDir + "\nСвои курсы (JSON): " + Path.Combine(AppPaths.DataDir, "courses");

        _loading = false;
    }

    void BuildThemes()
    {
        foreach (var theme in Themes.All)
        {
            var rb = new RadioButton
            {
                Content = theme.Name, GroupName = "theme", Margin = new Thickness(0, 2, 0, 2),
                IsChecked = theme.Name == App.Settings.Theme,
            };
            rb.Checked += (_, _) =>
            {
                if (_loading) return;
                App.Settings.Theme = theme.Name;
                Themes.Apply(theme);
            };
            ThemePanel.Children.Add(rb);
        }
    }

    void BuildPacks()
    {
        foreach (var (id, title) in Packs)
        {
            var rb = new RadioButton
            {
                Content = title, GroupName = "pack", Margin = new Thickness(18, 2, 0, 2),
                IsChecked = id == App.Settings.SoundPack,
            };
            var packId = id;
            rb.Checked += (_, _) =>
            {
                if (_loading) return;
                App.Settings.SoundPack = packId;
                App.Sound.Reload();
                App.Sound.Play(SoundKind.Wrong);
            };
            PackPanel.Children.Add(rb);
        }
    }

    void BuildTests()
    {
        foreach (var (kind, title) in Tests)
        {
            var k = kind;
            var button = new Button { Content = title, Style = (Style)FindResource("FlatButton"), Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) =>
            {
                bool was = App.Settings.SoundEnabled;
                App.Settings.SoundEnabled = true;   // кнопка «прослушать» работает даже при выключенном звуке
                App.Sound.Play(k);
                App.Settings.SoundEnabled = was;
            };
            TestPanel.Children.Add(button);
        }
    }

    void OnSoundToggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.SoundEnabled = SoundCheck.IsChecked == true;
    }

    void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        App.Settings.SoundVolume = VolumeSlider.Value / 100.0;
        App.Sound.Reload();
    }

    void OnMusicToggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.MusicEnabled = MusicCheck.IsChecked == true;
        App.Music.Apply();
    }

    void OnChooseMusic(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Аудио|*.mp3;*.wav;*.wma;*.m4a;*.flac|Все файлы|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        App.Settings.MusicPath = dialog.FileName;
        App.Settings.MusicEnabled = true;
        MusicCheck.IsChecked = true;
        UpdateMusicName();
        App.Music.Apply();
    }

    void UpdateMusicName() =>
        MusicName.Text = string.IsNullOrEmpty(App.Settings.MusicPath) ? "файл не выбран" : Path.GetFileName(App.Settings.MusicPath);

    void OnViewToggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.ShowKeyboard = KbCheck.IsChecked == true;
        App.Settings.SequentialUnlock = SeqCheck.IsChecked == true;
    }

    void OnReset(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            $"Сбросить весь прогресс и статистику курса «{_course.Title}»?\nЭто действие нельзя отменить.",
            "Сброс прогресса", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) App.Progress.ResetCourse(_course.Id);
    }

    void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        SettingsStore.Save(App.Settings);
        base.OnClosed(e);
    }
}
