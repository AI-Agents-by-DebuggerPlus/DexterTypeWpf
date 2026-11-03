using System.Text.Json;
using System.IO;

namespace DexterType;

public record HistoryEntry(DateTime When, string Layout, int Lesson, int Cpm, int Accuracy, int Errors);

public static class HistoryStore
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DexterType", "history.json");

    public static List<HistoryEntry> Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { return new(); }
    }

    public static void Save(List<HistoryEntry> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(items.TakeLast(100)));
        }
        catch { /* история не критична */ }
    }
}
