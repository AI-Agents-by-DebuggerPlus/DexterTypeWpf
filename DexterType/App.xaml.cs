using System.Windows;
using Dexter.Core.Storage;
using DexterType.Services;
using DexterType.Theming;

namespace DexterType;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static ProgressStore Progress { get; private set; } = null!;
    public static SoundService Sound { get; private set; } = null!;
    public static MusicService Music { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ev) =>
        {
            MessageBox.Show(ev.Exception.Message, "DexterType", MessageBoxButton.OK, MessageBoxImage.Error);
            ev.Handled = true;
        };

        Settings = SettingsStore.Load();
        Progress = new ProgressStore();
        Sound = new SoundService(Settings);
        Music = new MusicService(Settings);
        Themes.Apply(Themes.ByName(Settings.Theme));

        var window = new DexterType.MainWindow();
        MainWindow = window;
        window.Show();
        Music.Apply();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsStore.Save(Settings);
        base.OnExit(e);
    }
}
