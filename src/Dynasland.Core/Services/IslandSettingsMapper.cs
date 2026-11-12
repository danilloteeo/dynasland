using System.Globalization;
using System.Text.RegularExpressions;
using Dynasland.Core.Models;

namespace Dynasland.Core.Services;

/// <summary>Converte <see cref="IslandSettings"/> de/para pares chave/valor, tolerando valores inválidos.</summary>
public static partial class IslandSettingsMapper
{
    public static class Keys
    {
        public const string MonitorDeviceName = "island.monitor";
        public const string TopMargin = "island.topMargin";
        public const string Position = "island.position";
        public const string Shape = "island.shape";
        public const string IsVisible = "island.visible";
        public const string StartHidden = "island.startHidden";
        public const string HideInFullscreen = "island.hideInFullscreen";
        public const string ExpandedDurationSeconds = "island.expandedDurationSeconds";
        public const string AnimationsEnabled = "animations.enabled";
        public const string AnimationSpeed = "animations.speed";
        public const string AnimationIntensity = "animations.intensity";
        public const string Theme = "appearance.theme";
        public const string IslandTheme = "appearance.islandTheme";
        public const string IslandThemeFollowsMedia = "appearance.islandThemeFollowsMedia";
        public const string IdleDisplay = "appearance.idleDisplay";
        public const string AccentColor = "appearance.accent";
        public const string IslandOpacity = "appearance.opacity";
        public const string ShadowIntensity = "appearance.shadow";
        public const string Roundness = "appearance.roundness";
        public const string CompactWidth = "appearance.compactWidth";
        public const string CompactHeight = "appearance.compactHeight";
        public const string MaxWidth = "appearance.maxWidth";
        public const string CheckForUpdates = "system.checkForUpdates";
        public const string CollapseOnClickOutside = "behavior.collapseOnClickOutside";
        public const string DoubleClickAction = "behavior.doubleClickAction";
        public const string PinnedModuleId = "behavior.pinnedModule";
        public const string ClipboardHistoryEnabled = "privacy.clipboardHistory";
        public const string ClipboardStoreImages = "privacy.clipboardImages";
        public const string ClipboardMaxItems = "storage.clipboardMaxItems";
        public const string NotificationHistoryEnabled = "privacy.notificationHistory";
        public const string NotificationHistoryDays = "storage.notificationHistoryDays";
        public const string WeatherLocation = "weather.location";
        public const string SpotifyClientId = "spotify.clientId";
    }

    public static IslandSettings FromValues(IReadOnlyDictionary<string, string> values)
    {
        var d = IslandSettings.Default;
        values.TryGetValue(Keys.MonitorDeviceName, out var monitor);
        values.TryGetValue(Keys.AccentColor, out var accent);
        values.TryGetValue(Keys.PinnedModuleId, out var pinned);
        values.TryGetValue(Keys.WeatherLocation, out var weather);
        values.TryGetValue(Keys.SpotifyClientId, out var spotify);

        return new IslandSettings
        {
            MonitorDeviceName = string.IsNullOrWhiteSpace(monitor) ? null : monitor,
            TopMargin = ReadDouble(values, Keys.TopMargin, d.TopMargin, 0, 200),
            Position = ReadEnum(values, Keys.Position, d.Position),
            Shape = ReadEnum(values, Keys.Shape, d.Shape),
            IsVisible = ReadBool(values, Keys.IsVisible, d.IsVisible),
            StartHidden = ReadBool(values, Keys.StartHidden, d.StartHidden),
            HideInFullscreen = ReadBool(values, Keys.HideInFullscreen, d.HideInFullscreen),
            ExpandedDurationSeconds = ReadDouble(values, Keys.ExpandedDurationSeconds, d.ExpandedDurationSeconds, 1, 60),
            AnimationsEnabled = ReadBool(values, Keys.AnimationsEnabled, d.AnimationsEnabled),
            AnimationSpeed = ReadDouble(values, Keys.AnimationSpeed, d.AnimationSpeed, 0.25, 3),
            AnimationIntensity = ReadDouble(values, Keys.AnimationIntensity, d.AnimationIntensity, 0, 2),
            Theme = values.TryGetValue(Keys.Theme, out var theme) && Enum.TryParse<AppTheme>(theme, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : d.Theme,
            IslandTheme = values.TryGetValue(Keys.IslandTheme, out var island) && Enum.TryParse<IslandTheme>(island, ignoreCase: true, out var islandTheme) && Enum.IsDefined(islandTheme)
                ? islandTheme
                : d.IslandTheme,
            IslandThemeFollowsMedia = ReadBool(values, Keys.IslandThemeFollowsMedia, d.IslandThemeFollowsMedia),
            IdleDisplay = values.TryGetValue(Keys.IdleDisplay, out var idle) && Enum.TryParse<IdleDisplay>(idle, ignoreCase: true, out var idleDisplay) && Enum.IsDefined(idleDisplay)
                ? idleDisplay
                : d.IdleDisplay,
            AccentColor = accent is not null && IsHexColor(accent) ? accent.ToUpperInvariant() : d.AccentColor,
            IslandOpacity = ReadDouble(values, Keys.IslandOpacity, d.IslandOpacity, 0.5, 1),
            ShadowIntensity = ReadDouble(values, Keys.ShadowIntensity, d.ShadowIntensity, 0, 1),
            Roundness = ReadDouble(values, Keys.Roundness, d.Roundness, 0.3, 1),
            CompactWidth = ReadDouble(values, Keys.CompactWidth, d.CompactWidth, 140, 300),
            CompactHeight = ReadDouble(values, Keys.CompactHeight, d.CompactHeight, 28, 44),
            MaxWidth = ReadDouble(values, Keys.MaxWidth, d.MaxWidth, 340, 420),
            CheckForUpdates = ReadBool(values, Keys.CheckForUpdates, d.CheckForUpdates),
            CollapseOnClickOutside = ReadBool(values, Keys.CollapseOnClickOutside, d.CollapseOnClickOutside),
            DoubleClickAction = values.TryGetValue(Keys.DoubleClickAction, out var dbl) && Enum.TryParse<DoubleClickAction>(dbl, ignoreCase: true, out var action) && Enum.IsDefined(action)
                ? action
                : d.DoubleClickAction,
            PinnedModuleId = string.IsNullOrWhiteSpace(pinned) ? null : pinned,
            ClipboardHistoryEnabled = ReadBool(values, Keys.ClipboardHistoryEnabled, d.ClipboardHistoryEnabled),
            ClipboardStoreImages = ReadBool(values, Keys.ClipboardStoreImages, d.ClipboardStoreImages),
            ClipboardMaxItems = (int)ReadDouble(values, Keys.ClipboardMaxItems, d.ClipboardMaxItems, 20, 2000),
            NotificationHistoryEnabled = ReadBool(values, Keys.NotificationHistoryEnabled, d.NotificationHistoryEnabled),
            NotificationHistoryDays = (int)ReadDouble(values, Keys.NotificationHistoryDays, d.NotificationHistoryDays, 1, 90),
            WeatherLocation = string.IsNullOrWhiteSpace(weather) ? null : weather,
            SpotifyClientId = string.IsNullOrWhiteSpace(spotify) ? null : spotify.Trim(),
        };
    }

    public static IReadOnlyDictionary<string, string> ToValues(IslandSettings s) => new Dictionary<string, string>
    {
        [Keys.MonitorDeviceName] = s.MonitorDeviceName ?? string.Empty,
        [Keys.TopMargin] = Format(s.TopMargin),
        [Keys.Position] = s.Position.ToString(),
        [Keys.Shape] = s.Shape.ToString(),
        [Keys.IsVisible] = Format(s.IsVisible),
        [Keys.StartHidden] = Format(s.StartHidden),
        [Keys.HideInFullscreen] = Format(s.HideInFullscreen),
        [Keys.ExpandedDurationSeconds] = Format(s.ExpandedDurationSeconds),
        [Keys.AnimationsEnabled] = Format(s.AnimationsEnabled),
        [Keys.AnimationSpeed] = Format(s.AnimationSpeed),
        [Keys.AnimationIntensity] = Format(s.AnimationIntensity),
        [Keys.Theme] = s.Theme.ToString(),
        [Keys.IslandTheme] = s.IslandTheme.ToString(),
        [Keys.IslandThemeFollowsMedia] = Format(s.IslandThemeFollowsMedia),
        [Keys.IdleDisplay] = s.IdleDisplay.ToString(),
        [Keys.AccentColor] = s.AccentColor,
        [Keys.IslandOpacity] = Format(s.IslandOpacity),
        [Keys.ShadowIntensity] = Format(s.ShadowIntensity),
        [Keys.Roundness] = Format(s.Roundness),
        [Keys.CompactWidth] = Format(s.CompactWidth),
        [Keys.CompactHeight] = Format(s.CompactHeight),
        [Keys.MaxWidth] = Format(s.MaxWidth),
        [Keys.CheckForUpdates] = Format(s.CheckForUpdates),
        [Keys.CollapseOnClickOutside] = Format(s.CollapseOnClickOutside),
        [Keys.DoubleClickAction] = s.DoubleClickAction.ToString(),
        [Keys.PinnedModuleId] = s.PinnedModuleId ?? string.Empty,
        [Keys.ClipboardHistoryEnabled] = Format(s.ClipboardHistoryEnabled),
        [Keys.ClipboardStoreImages] = Format(s.ClipboardStoreImages),
        [Keys.ClipboardMaxItems] = Format(s.ClipboardMaxItems),
        [Keys.NotificationHistoryEnabled] = Format(s.NotificationHistoryEnabled),
        [Keys.NotificationHistoryDays] = Format(s.NotificationHistoryDays),
        [Keys.WeatherLocation] = s.WeatherLocation ?? string.Empty,
        [Keys.SpotifyClientId] = s.SpotifyClientId ?? string.Empty,
    };

    public static bool IsHexColor(string value) => HexColor().IsMatch(value);

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Format(bool value) => value ? "true" : "false";

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

    /// <summary>Nome do valor (sem diferenciar maiúsculas); número ou nome desconhecido volta ao padrão.</summary>
    private static T ReadEnum<T>(IReadOnlyDictionary<string, string> values, string key, T fallback) where T : struct, Enum =>
        values.TryGetValue(key, out var raw) && Enum.TryParse<T>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
