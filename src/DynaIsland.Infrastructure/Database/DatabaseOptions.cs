namespace DynaIsland.Infrastructure.Database;

public sealed record DatabaseOptions(string DatabasePath)
{
    /// <summary>%LocalAppData%\DynaIsland\dynaisland.db</summary>
    public static string DefaultPath => Path.Combine(DefaultDirectory, "dynaisland.db");

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynaIsland");

    /// <summary>%LocalAppData%\Dynasland: pasta do aplicativo até a 1.7.1 (ver <see cref="LegacyDataMigration"/>).</summary>
    public static string LegacyDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dynasland");

    public static string LegacyPath => Path.Combine(LegacyDirectory, "dynasland.db");
}
