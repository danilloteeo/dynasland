using Dynasland.Core.Models;

// Contratos de utilidades: métricas do sistema, downloads, área de transferência, shell e som de alerta.

namespace Dynasland.Core.Interfaces;

public interface ISystemMetricsService
{
    /// <summary>Leitura completa (CPU, GPU, temperaturas, memória, discos). Chamar fora da thread de UI.</summary>
    SystemMetrics Sample();

    /// <summary>Leitura barata para verificações em segundo plano.</summary>
    MemoryUsage GetMemory();

    IReadOnlyList<DiskUsage> GetDisks();

    /// <summary>Velocidade de rede desde a chamada anterior (leitura barata); <c>null</c> se ainda não há base de comparação.</summary>
    NetworkRate? GetNetworkRate() => null;
}

/// <summary>Verso de uma letra sincronizada: aparece a partir de <paramref name="Time"/>.</summary>
public sealed record LyricLine(TimeSpan Time, string Text);

/// <summary>Letras sincronizadas de músicas, de um serviço na internet.</summary>
public interface ILyricsService
{
    /// <returns>Versos em ordem de tempo; <c>null</c> se a música não tem letra sincronizada ou o serviço falhou.</returns>
    Task<IReadOnlyList<LyricLine>?> GetAsync(string title, string artist, string? album, TimeSpan? duration, CancellationToken cancellationToken = default);
}

public enum BluetoothDeviceKind
{
    Other,
    Mouse,
    Keyboard,
    Headset,
    Gamepad,
}

/// <summary>Dispositivo Bluetooth pareado.</summary>
public sealed record BluetoothDeviceInfo(string Id, string Name, bool IsConnected, BluetoothDeviceKind Kind = BluetoothDeviceKind.Other);

/// <summary>Descobre o tipo do dispositivo pela classe Bluetooth clássica, pela aparência do BLE ou, na falta delas, pelo nome.</summary>
public static class BluetoothDeviceClassifier
{
    private const uint AudioVideoMajor = 4;
    private const uint PeripheralMajor = 5;
    private const uint HidAppearanceCategory = 15;

    /// <param name="major">Classe principal do dispositivo (Cod.Major).</param>
    /// <param name="minor">Classe secundária de 6 bits (Cod.Minor).</param>
    /// <param name="appearanceCategory">Categoria da aparência BLE (Appearance.Category).</param>
    /// <param name="appearanceSub">Subcategoria da aparência BLE (Appearance.SubCategory).</param>
    public static BluetoothDeviceKind Classify(uint? major, uint? minor, uint? appearanceCategory, uint? appearanceSub, string name)
    {
        if (major == AudioVideoMajor)
        {
            return BluetoothDeviceKind.Headset;
        }

        if (major == PeripheralMajor && minor is { } cod)
        {
            // Bits altos do campo secundário: 01 teclado, 10 apontador, 11 combo (teclado e mouse).
            var kind = (cod >> 4) & 0b11;
            var low = cod & 0b1111;
            if (low is 1 or 2)
            {
                return BluetoothDeviceKind.Gamepad;
            }

            if (kind == 0b10)
            {
                return BluetoothDeviceKind.Mouse;
            }

            if (kind is 0b01 or 0b11)
            {
                return BluetoothDeviceKind.Keyboard;
            }
        }

        if (appearanceCategory == HidAppearanceCategory)
        {
            return appearanceSub switch
            {
                1 => BluetoothDeviceKind.Keyboard,
                2 => BluetoothDeviceKind.Mouse,
                3 or 4 => BluetoothDeviceKind.Gamepad,
                _ => FromName(name),
            };
        }

        return FromName(name);
    }

    private static BluetoothDeviceKind FromName(string name)
    {
        if (Has(name, "mouse") || Has(name, "mx master") || Has(name, "trackpad"))
        {
            return BluetoothDeviceKind.Mouse;
        }

        if (Has(name, "keyboard") || Has(name, "teclado") || Has(name, "keys"))
        {
            return BluetoothDeviceKind.Keyboard;
        }

        if (Has(name, "headset") || Has(name, "headphone") || Has(name, "earbuds") || Has(name, "airpods") || Has(name, "fone"))
        {
            return BluetoothDeviceKind.Headset;
        }

        return Has(name, "controller") || Has(name, "gamepad") ? BluetoothDeviceKind.Gamepad : BluetoothDeviceKind.Other;
    }

    private static bool Has(string name, string part) => name.Contains(part, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Dispositivos Bluetooth pareados: conexão, desconexão e bateria (quando o Windows informa).</summary>
public interface IBluetoothService
{
    /// <summary>Um dispositivo pareado conectou ou desconectou (qualquer thread).</summary>
    event EventHandler<BluetoothDeviceInfo>? ConnectionChanged;

    IReadOnlyList<BluetoothDeviceInfo> Devices { get; }

    /// <returns><c>false</c> se o computador não tem adaptador Bluetooth.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    /// <summary>Bateria em porcentagem; <c>null</c> quando o dispositivo ou o Windows não informam.</summary>
    Task<int?> GetBatteryAsync(string deviceId);
}

/// <summary>
/// Fonte de downloads. Navegadores não expõem seus downloads de forma padronizada, então cada
/// integração (pasta Downloads, extensão de navegador, gerenciador de downloads...) é um provedor.
/// </summary>
public interface IDownloadProvider
{
    string Name { get; }

    event EventHandler<DownloadInfo>? Changed;

    /// <returns><c>false</c> se o provedor não puder funcionar neste computador.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

public interface IClipboardService
{
    /// <summary>
    /// O conteúdo mudou. Não é disparado para conteúdo marcado como privado
    /// (ex.: senhas copiadas por gerenciadores de senha).
    /// </summary>
    event EventHandler? ContentChanged;

    /// <summary>Histórico da área de transferência ativado nas configurações do Windows (Win+V).</summary>
    bool IsHistoryEnabled { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    Task<ClipboardEntry?> GetCurrentAsync();

    /// <summary>Itens mais recentes do histórico do Windows; vazio se o histórico estiver desativado.</summary>
    Task<IReadOnlyList<ClipboardEntry>> GetHistoryAsync(int maxItems);

    /// <summary>Coloca novamente na área de transferência um item do histórico.</summary>
    Task<bool> CopyAgainAsync(string entryId);

    Task<bool> SetTextAsync(string text);

    /// <param name="image">Imagem codificada (PNG, BMP...).</param>
    Task<bool> SetImageAsync(byte[] image);
}

/// <summary>Ações de shell: abrir arquivos e mostrá-los no Explorador.</summary>
public interface IShellService
{
    bool Open(string path);

    bool RevealInFolder(string path);

    /// <summary>Abre um endereço https no navegador padrão.</summary>
    bool OpenUrl(string url);
}

/// <summary>Som de alerta (ex.: fim do temporizador).</summary>
public interface IAlertSound
{
    void Play();
}
