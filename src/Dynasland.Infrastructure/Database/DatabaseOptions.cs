namespace Dynasland.Infrastructure.Database;

public sealed record DatabaseOptions(string DatabasePath)
{
    /// <summary>%LocalAppData%\Dynasland\dynasland.db</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dynasland",
        "dynasland.db");
}
