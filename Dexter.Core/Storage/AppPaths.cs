namespace Dexter.Core.Storage;

public static class AppPaths
{
    /// <summary>Папка данных пользователя. Можно переопределить (тесты, портативный режим).</summary>
    public static string DataDir { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DexterType");

    public static string File(string name) => Path.Combine(DataDir, name);
}
