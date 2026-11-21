using System.Runtime.InteropServices;
using Dexter.Core.Audio;
using Dexter.Core.Storage;

namespace DexterType.Services;

/// <summary>
/// Воспроизводит синтезированные звуки через winmm PlaySound (SND_MEMORY | SND_ASYNC):
/// низкая задержка, быстрые повторы обрывают предыдущий звук, внешние файлы не нужны.
/// Буферы лежат в Pinned Object Heap, поэтому адрес не меняется во время асинхронного воспроизведения.
/// </summary>
public sealed class SoundService
{
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true)]
    static extern bool PlaySound(byte[]? pszSound, IntPtr hmod, uint fdwSound);

    const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_MEMORY = 0x0004;

    readonly AppSettings _settings;
    readonly Dictionary<SoundKind, byte[]> _cache = new();
    readonly List<byte[]> _retired = new();   // старые буферы живут до конца работы: они могли ещё играть

    public SoundService(AppSettings settings) => _settings = settings;

    /// <summary>Вызывать после смены набора звуков или громкости.</summary>
    public void Reload()
    {
        _retired.AddRange(_cache.Values);
        _cache.Clear();
    }

    public void Play(SoundKind kind)
    {
        if (!_settings.SoundEnabled || _settings.SoundVolume <= 0) return;
        try
        {
            PlaySound(Get(kind), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
        }
        catch
        {
            // Звук — удобство, а не необходимость: любой сбой игнорируем.
        }
    }

    byte[] Get(SoundKind kind)
    {
        if (_cache.TryGetValue(kind, out var cached)) return cached;
        var wav = SoundSynth.Render(_settings.SoundPack, kind, _settings.SoundVolume);
        var pinned = GC.AllocateUninitializedArray<byte>(wav.Length, pinned: true);
        wav.CopyTo(pinned, 0);
        return _cache[kind] = pinned;
    }
}
