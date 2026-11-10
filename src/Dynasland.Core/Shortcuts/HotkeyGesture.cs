using System.Globalization;

namespace Dynasland.Core.Shortcuts;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// Combinação de teclas global (ex.: <c>Ctrl+Alt+D</c>). <see cref="Key"/> é o código de tecla virtual
/// do Windows. Exige ao menos um modificador além de Shift, para não roubar teclas comuns.
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int Key)
{
    private static readonly Dictionary<int, string> KeyNames = BuildKeyNames();
    private static readonly Dictionary<string, int> KeysByName = KeyNames.ToDictionary(p => p.Value, p => p.Key, StringComparer.OrdinalIgnoreCase);

    public bool IsValid => Key != 0
        && (Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) != 0
        && !IsModifierKey(Key);

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(Key));
        return string.Join('+', parts);
    }

    public static string KeyName(int key) => KeyNames.TryGetValue(key, out var name)
        ? name
        : "0x" + key.ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Lê o formato de <see cref="ToString"/>; <c>null</c> se inválido.</summary>
    public static HotkeyGesture? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var modifiers = HotkeyModifiers.None;
        int? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "WIN" or "WINDOWS":
                    modifiers |= HotkeyModifiers.Windows;
                    break;
                default:
                    if (key is not null)
                    {
                        return null;
                    }

                    if (KeysByName.TryGetValue(raw, out var named))
                    {
                        key = named;
                    }
                    else if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(raw.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                    {
                        key = code;
                    }
                    else
                    {
                        return null;
                    }

                    break;
            }
        }

        var gesture = key is { } k ? new HotkeyGesture(modifiers, k) : null;
        return gesture is { IsValid: true } ? gesture : null;
    }

    private static bool IsModifierKey(int key) =>
        key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    private static Dictionary<int, string> BuildKeyNames()
    {
        var names = new Dictionary<int, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x1B] = "Esc",
            [0x20] = "Espaço",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Esquerda",
            [0x26] = "Cima",
            [0x27] = "Direita",
            [0x28] = "Baixo",
            [0x2C] = "PrintScreen",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0xBA] = ";",
            [0xBB] = "=",
            [0xBC] = ",",
            [0xBD] = "-",
            [0xBE] = ".",
            [0xBF] = "/",
            [0xC0] = "`",
            [0xDB] = "[",
            [0xDC] = "\\",
            [0xDD] = "]",
            [0xDE] = "'",
        };

        for (var c = '0'; c <= '9'; c++)
        {
            names[c] = c.ToString();
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            names[c] = c.ToString();
        }

        for (var i = 1; i <= 24; i++)
        {
            names[0x6F + i] = $"F{i}";
        }

        for (var i = 0; i <= 9; i++)
        {
            names[0x60 + i] = $"Num{i}";
        }

        return names;
    }
}
