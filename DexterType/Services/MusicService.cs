using System.IO;
using System.Windows.Media;
using Dexter.Core.Storage;

namespace DexterType.Services;

/// <summary>Фоновая музыка по желанию пользователя: один файл по кругу, тихо.</summary>
public sealed class MusicService
{
    readonly MediaPlayer _player = new();
    readonly AppSettings _settings;
    string? _playing;

    public MusicService(AppSettings settings)
    {
        _settings = settings;
        _player.Volume = 0.25;
        _player.MediaEnded += (_, _) => { _player.Position = TimeSpan.Zero; _player.Play(); };
    }

    public void Apply()
    {
        bool want = _settings.MusicEnabled && File.Exists(_settings.MusicPath);
        if (!want)
        {
            _player.Stop();
            _playing = null;
            return;
        }
        if (_playing == _settings.MusicPath) return;
        _player.Open(new Uri(_settings.MusicPath));
        _player.Play();
        _playing = _settings.MusicPath;
    }
}
