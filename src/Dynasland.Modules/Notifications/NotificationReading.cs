namespace Dynasland.Modules.Notifications;

/// <summary>Quanto texto cabe no aviso e por quanto tempo ele deve ficar na tela para dar tempo de ler.</summary>
public static class NotificationReading
{
    public const int MaxLines = 3;

    /// <summary>Caracteres que cabem em uma linha do corpo (12 px, cartão de 400 px com o logo).</summary>
    public const int CharsPerLine = 46;

    /// <summary>Altura de cada linha do corpo, em DIPs.</summary>
    public const double LineHeight = 16;

    private const double BaseHeight = 72;
    private const double ExtraHeightPerLine = 20;
    private const double SecondsPerCharacter = 0.04;
    private const double MaxSeconds = 12;
    private const double HighPriorityBonusSeconds = 2;

    /// <summary>Linhas que o corpo ocupa (de 1 a <see cref="MaxLines"/>), contando o texto em uma linha corrida.</summary>
    public static int Lines(string body)
    {
        var length = body.Trim().Length;
        return Math.Clamp((int)Math.Ceiling(length / (double)CharsPerLine), 1, MaxLines);
    }

    /// <summary>Altura da linha de conteúdo do aviso: 72, 92 ou 112.</summary>
    public static double Height(int lines) => BaseHeight + ExtraHeightPerLine * (Math.Clamp(lines, 1, MaxLines) - 1);

    /// <summary>Tempo base do aplicativo mais 40 ms por caractere, no máximo 12 s; prioridade alta ganha 2 s a mais.</summary>
    public static TimeSpan Duration(TimeSpan baseDuration, int bodyLength, bool highPriority)
    {
        var seconds = Math.Min(MaxSeconds, baseDuration.TotalSeconds + SecondsPerCharacter * Math.Max(0, bodyLength));
        seconds = Math.Max(seconds, baseDuration.TotalSeconds);
        return TimeSpan.FromSeconds(seconds + (highPriority ? HighPriorityBonusSeconds : 0));
    }
}
