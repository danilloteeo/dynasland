namespace Dynasland.Core.Services;

public static class ReleaseVersion
{
    /// <summary>Lê versões no formato de tags (<c>v1.2.3</c>, <c>1.2</c>, <c>v1.2.3-beta</c>).</summary>
    public static Version? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().TrimStart('v', 'V');
        var suffix = text.IndexOfAny(['-', '+', ' ']);
        if (suffix >= 0)
        {
            text = text[..suffix];
        }

        if (!Version.TryParse(text.Contains('.') ? text : text + ".0", out var version))
        {
            return null;
        }

        // Normaliza componentes ausentes (-1) para comparar 1.2 == 1.2.0.
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    public static bool IsNewer(string? latestTag, Version current) =>
        Parse(latestTag) is { } latest && latest > new Version(current.Major, current.Minor, Math.Max(0, current.Build));
}
