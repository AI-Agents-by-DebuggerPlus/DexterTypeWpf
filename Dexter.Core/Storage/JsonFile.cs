using System.Text.Json;

namespace Dexter.Core.Storage;

public static class JsonFile
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static T Load<T>(string path) where T : new()
    {
        try
        {
            if (!File.Exists(path)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch
        {
            // Повреждённый файл не должен ронять приложение; оставляем копию для разбора.
            try { File.Copy(path, path + ".broken", overwrite: true); } catch { }
            return new T();
        }
    }

    /// <summary>Атомарная запись: во временный файл, затем замена.</summary>
    public static bool Save<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch { return false; }
    }
}
