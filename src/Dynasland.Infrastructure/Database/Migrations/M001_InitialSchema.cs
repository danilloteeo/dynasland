namespace Dynasland.Infrastructure.Database.Migrations;

public sealed class M001_InitialSchema : SqlMigration
{
    public override int Version => 1;

    public override string Name => "Esquema inicial";

    protected override string Sql => """
        CREATE TABLE settings (
            key        TEXT PRIMARY KEY NOT NULL,
            value      TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE modules (
            id         TEXT PRIMARY KEY NOT NULL,
            enabled    INTEGER NOT NULL DEFAULT 1,
            updated_at TEXT NOT NULL
        );
        """;
}
