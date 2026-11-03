using System.Windows;
using System.Windows.Media;

namespace DexterType;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        if (Application.Current.MainWindow is MainWindow mw)
        {
            MusicEnabledCheck.IsChecked = mw._musicPlayer.Source != null && mw._musicPlayer.CanPause; 
            // More robust check: use the actual mute state if we had one, but checking if playing is fine.
            // For now, let's just default it based on if a file is loaded.
            MusicEnabledCheck.IsChecked = System.IO.File.Exists("music_on.txt") 
                ? System.IO.File.ReadAllText("music_on.txt") == "1" 
                : true;
        }
    }

    private void SetCandyTheme(object sender, RoutedEventArgs e) => ApplyTheme(
        "#F8F9FB", "White", "#2C3E50", 
        "#B5A2D8", "#A5D6A7", "#F48FB1", "#80DEEA", "#FFE082");

    private void SetDarkTheme(object sender, RoutedEventArgs e) => ApplyTheme(
        "#1A1A1A", "#2D2D2D", "#E0E0E0", 
        "#4A148C", "#1B5E20", "#880E4F", "#006064", "#E65100");

    private void SetMonoTheme(object sender, RoutedEventArgs e) => ApplyTheme(
        "#F5F5F5", "White", "Black", 
        "#D0D0D0", "#C0C0C0", "#B0B0B0", "#A0A0A0", "#909090");

    private void SetStaminaTheme(object sender, RoutedEventArgs e) => ApplyTheme(
        "#2C3E50", "Transparent", "#ECF0F1", 
        "#3498DB", "#2ECC71", "#E74C3C", "#F1C40F", "#95A5A6");

    private void SetCyberTheme(object sender, RoutedEventArgs e) => ApplyTheme(
        "#0D0208", "#003B46", "#00FF41", 
        "#00FF41", "#008F11", "#003B46", "#00FF41", "#008F11");

    private void ApplyTheme(string bg, string panel, string text, string c1, string c2, string c3, string c4, string c5)
    {
        var res = Application.Current.Resources;
        res["ThemeBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
        res["ThemeTextPanel"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(panel));
        res["ThemeMainText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(text));
        
        res["ColorPinky"] = (Color)ColorConverter.ConvertFromString(c1);
        res["ColorRing"] = (Color)ColorConverter.ConvertFromString(c2);
        res["ColorMiddle"] = (Color)ColorConverter.ConvertFromString(c3);
        res["ColorIndex"] = (Color)ColorConverter.ConvertFromString(c4);
        res["ColorThumb"] = (Color)ColorConverter.ConvertFromString(c5);

        if (Owner is MainWindow mw) mw.BuildKeyboard(mw.CurrentLayout);
    }
    
    private void ToggleKeyboard(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mw)
        {
            mw.KbArea.Visibility = mw.KbArea.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void MusicChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Owner is not MainWindow mw || MusicBox == null || !this.IsLoaded) return;
        if (MusicEnabledCheck.IsChecked == false) return;

        var item = (System.Windows.Controls.ComboBoxItem)MusicBox.SelectedItem;
        if (item.Content.ToString() == "Выбрать файл...")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Audio files|*.mp3;*.wav" };
            if (dialog.ShowDialog() == true) mw.PlayMusic(dialog.FileName);
            else MusicBox.SelectedIndex = 0;
        }
        else mw.PlayMusic(null);
    }

    private void MusicToggleChanged(object sender, RoutedEventArgs e)
    {
        if (Owner is not MainWindow mw || !this.IsLoaded) return;
        bool isEnabled = MusicEnabledCheck.IsChecked ?? false;
        System.IO.File.WriteAllText("music_on.txt", isEnabled ? "1" : "0");
        
        if (isEnabled)
        {
            string musicPath = System.IO.File.Exists("music.txt") ? System.IO.File.ReadAllText("music.txt") : "";
            if (!string.IsNullOrEmpty(musicPath)) mw.PlayMusic(musicPath);
        }
        else
        {
            mw._musicPlayer.Stop();
        }
    }
}
