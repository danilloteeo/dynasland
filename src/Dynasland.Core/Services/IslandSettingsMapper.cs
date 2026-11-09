using System.Globalization;
using Dynasland.Core.Models;

namespace Dynasland.Core.Services;

/// <summary>Converte <see cref="IslandSettings"/> de/para pares chave/valor, tolerando valores inválidos.</summary>
public static class IslandSettingsMapper
{
    public static class Keys
    {
        public const string MonitorDeviceName = "island.monitor";
        public const string TopMargin = "island.topMargin";
        public const string IsVisible = "island.visible";
        public const string ExpandedDurationSeconds = "island.expandedDurationSeconds";
        public const string AnimationsEnabled = "animations.enabled";
        public const string AnimationSpeed = "animations.speed";
    }

    public static IslandSettings FromValues(IReadOnlyDictionary<string, string> values)
    {
        var defaults = IslandSettings.Default;
        values.TryGetValue(Keys.MonitorDeviceName, out var monitor);

        return new IslandSettings
        {
            MonitorDeviceName = string.IsNullOrWhiteSpace(monitor) ? null : monitor,
            TopMargin = ReadDouble(values, Keys.TopMargin, defaults.TopMargin, 0, 200),
            IsVisible = ReadBool(values, Keys.IsVisible, defaults.IsVisible),
            ExpandedDurationSeconds = ReadDouble(values, Keys.ExpandedDurationSeconds, defaults.ExpandedDurationSeconds, 1, 60),
            AnimationsEnabled = ReadBool(values, Keys.AnimationsEnabled, defaults.AnimationsEnabled),
            AnimationSpeed = ReadDouble(values, Keys.AnimationSpeed, defaults.AnimationSpeed, 0.25, 3),
        };
    }

    public static IReadOnlyDictionary<string, string> ToValues(IslandSettings settings) => new Dictionary<string, string>
    {
        [Keys.MonitorDeviceName] = settings.MonitorDeviceName ?? string.Empty,
        [Keys.TopMargin] = settings.TopMargin.ToString(CultureInfo.InvariantCulture),
        [Keys.IsVisible] = settings.IsVisible ? "true" : "false",
        [Keys.ExpandedDurationSeconds] = settings.ExpandedDurationSeconds.ToString(CultureInfo.InvariantCulture),
        [Keys.AnimationsEnabled] = settings.AnimationsEnabled ? "true" : "false",
        [Keys.AnimationSpeed] = settings.AnimationSpeed.ToString(CultureInfo.InvariantCulture),
    };

    private static double ReadDouble(IReadOnlyDictionary<string, string> values, string key, double fallback, double min, double max)
    {
        if (values.TryGetValue(key, out var raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value))
        {
            return Math.Clamp(value, min, max);
        }

        return fallback;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;
}
