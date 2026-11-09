using System.IO;
using System.Media;
using Dynasland.Core.Interfaces;

namespace Dynasland.App.Services;

/// <summary>Toca o alarme padrão do Windows (Alarm01.wav) ou, sem ele, o som de exclamação do sistema.</summary>
public sealed class AlertSoundPlayer : IAlertSound, IDisposable
{
    private static readonly string AlarmPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Alarm01.wav");

    // Mantido vivo: descartar o SoundPlayer interromperia a reprodução assíncrona.
    private readonly SoundPlayer? _player = File.Exists(AlarmPath) ? new SoundPlayer(AlarmPath) : null;

    public void Play()
    {
        try
        {
            if (_player is not null)
            {
                _player.Play();
                return;
            }
        }
        catch (Exception)
        {
            // Arquivo inacessível ou dispositivo de áudio indisponível: recorre ao som do sistema.
        }

        SystemSounds.Exclamation.Play();
    }

    public void Dispose() => _player?.Dispose();
}
