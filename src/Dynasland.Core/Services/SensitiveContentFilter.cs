using System.Text.RegularExpressions;

namespace Dynasland.Core.Services;

/// <summary>
/// Heurísticas para não guardar no histórico textos que parecem dados sensíveis, além do que os próprios
/// aplicativos marcam como privado (gerenciadores de senha marcam a cópia e ela nem chega aqui).
/// </summary>
public static partial class SensitiveContentFilter
{
    /// <summary>Número de cartão (13 a 19 dígitos com dígito verificador válido), chave privada ou token longo.</summary>
    public static bool IsLikelySensitive(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Contains("PRIVATE KEY-----", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Length <= 23 && CardLike().IsMatch(trimmed))
        {
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits))
            {
                return true;
            }
        }

        // Token de acesso (JWT) ou chave de API: uma "palavra" longa sem espaços e de alta variedade.
        return trimmed.Length is >= 32 and <= 2048 && !trimmed.Contains(' ') && (Jwt().IsMatch(trimmed) || ApiKey().IsMatch(trimmed));
    }

    internal static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"^[\d \-]+$")]
    private static partial Regex CardLike();

    [GeneratedRegex(@"^eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+$")]
    private static partial Regex Jwt();

    // Prefixos conhecidos de chaves (GitHub, OpenAI/Anthropic, AWS, Slack, Google).
    [GeneratedRegex(@"^(ghp_|gho_|github_pat_|sk-|AKIA|xox[abp]-|AIza)[A-Za-z0-9_\-]{16,}$")]
    private static partial Regex ApiKey();
}
