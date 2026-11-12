namespace Dynasland.Core.Models;

/// <summary>Dispositivo protegido pela privacidade do Windows cujo uso a ilha indica.</summary>
public enum PrivacyCapability
{
    Microphone,
    Camera,
}

/// <summary>
/// Registro de uso de um aplicativo, como o Windows guarda em
/// <c>CapabilityAccessManager\ConsentStore</c>: horários de início e fim (FILETIME; 0 = nunca).
/// </summary>
/// <param name="Key">Nome da chave do aplicativo (empacotado: <c>Editor.App_hash</c>; desktop: caminho com <c>#</c> no lugar de <c>\</c>).</param>
public sealed record PrivacyUsageEntry(PrivacyCapability Capability, string Key, long Start, long Stop);

/// <summary>Microfone e câmera em uso agora e por quais aplicativos (nomes legíveis, sem repetição).</summary>
public sealed record PrivacyUsage(bool Microphone, bool Camera, IReadOnlyList<string> MicrophoneApps, IReadOnlyList<string> CameraApps)
{
    public static PrivacyUsage None { get; } = new(false, false, [], []);

    // Igualdade por conteúdo das listas: o serviço só avisa quando algo realmente mudou.
    public bool Equals(PrivacyUsage? other) =>
        other is not null
        && Microphone == other.Microphone
        && Camera == other.Camera
        && MicrophoneApps.SequenceEqual(other.MicrophoneApps)
        && CameraApps.SequenceEqual(other.CameraApps);

    public override int GetHashCode() => HashCode.Combine(Microphone, Camera, MicrophoneApps.Count, CameraApps.Count);
}
