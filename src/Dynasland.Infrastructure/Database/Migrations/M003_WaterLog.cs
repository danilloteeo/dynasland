namespace Dynasland.Infrastructure.Database.Migrations;

/// <summary>Copos de água por dia (data local, "yyyy-MM-dd").</summary>
public sealed class M003_WaterLog : SqlMigration
{
    public override int Version => 3;

    public override string Name => "Registro de água";

    protected override string Sql => """
        CREATE TABLE water_log (
            day     TEXT PRIMARY KEY NOT NULL,
            glasses INTEGER NOT NULL
        );
        """;
}
