using System.Data;
using Microsoft.EntityFrameworkCore;

namespace TextEnhancer.Api.Data;

/// <summary>
/// Stop-gap forward-only schema patches for environments where the existing SQLite file pre-dates a
/// new column. <see cref="DatabaseFacade.EnsureCreated"/> never migrates an existing database, and
/// Azure App Service persists <c>App_Data/</c> between deploys, so a freshly added column is missing
/// in production until someone patches it. Each patch must be idempotent and safe to re-run.
///
/// If the schema starts evolving regularly, replace this with EF Core Migrations.
/// </summary>
public static class SqliteSchemaPatch
{
    public static void Apply(AppDbContext db)
    {
        // The InMemory test provider isn't relational; PRAGMA / ALTER TABLE only make sense on SQLite.
        if (!db.Database.IsRelational()) return;

        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            EnsureColumn(conn, "Interactions", "EnhancedSectionsJson", "TEXT NULL");
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    private static void EnsureColumn(IDbConnection conn, string table, string column, string typeClause)
    {
        if (HasColumn(conn, table, column)) return;
        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeClause}";
        alter.ExecuteNonQuery();
    }

    private static bool HasColumn(IDbConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
