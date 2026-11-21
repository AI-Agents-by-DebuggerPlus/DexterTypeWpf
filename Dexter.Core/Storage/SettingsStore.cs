namespace Dexter.Core.Storage;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Stamina";
    public bool SoundEnabled { get; set; } = true;
    public double SoundVolume { get; set; } = 0.6;      // 0..1
    public string SoundPack { get; set; } = "classic";  // classic | soft | click | beep
    public bool MusicEnabled { get; set; }
    public string MusicPath { get; set; } = "";
    public bool ShowKeyboard { get; set; } = true;
    public bool SequentialUnlock { get; set; }           // false: свободная навигация, как в Stamina
    public string LastCourse { get; set; } = "";
    public string LastLesson { get; set; } = "";
}

public static class SettingsStore
{
    static string Path => AppPaths.File("settings.json");
    public static AppSettings Load() => JsonFile.Load<AppSettings>(Path);
    public static void Save(AppSettings s) => JsonFile.Save(Path, s);
}
