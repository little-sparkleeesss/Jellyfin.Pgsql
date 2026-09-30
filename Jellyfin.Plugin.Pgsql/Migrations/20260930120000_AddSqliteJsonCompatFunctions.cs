using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Plugin.Pgsql.Migrations;

/// <inheritdoc />
/// <summary>
/// Provides SQLite-compatible json_valid and json_remove in the database.
/// Jellyfins 20260911120000_StripEmbeddedLinkedChildren migration routine runs
/// hard coded SQLite SQL (json_remove with a json_valid guard) through
/// ExecuteSqlRaw. On PostgreSQL that fails with 42883 "function json_valid(text)
/// does not exist", which aborts the upgrade and leaves the server unable to
/// start. Defining the two functions here lets that statement run unmodified.
/// EF migrations run in the CoreInitialisation stage, before the routine (which
/// is an AppInitialisation routine), so the functions are in place in time.
/// </summary>
[DbContext(typeof(JellyfinDbContext))]
[Migration("20260930120000_AddSqliteJsonCompatFunctions")]
public partial class AddSqliteJsonCompatFunctions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop first rather than CREATE OR REPLACE: replacing a function whose return type differs
        // fails, and that failure would block startup - the exact outcome this migration prevents.
        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS json_valid(text);
            DROP FUNCTION IF EXISTS json_remove(text, text[]);
            """);

        // Returns integer, not boolean, because the caller compares the result with = 1. An invalid
        // document returns 0 instead of raising, so one malformed row cannot abort the statement.
        // PL/pgSQL is used rather than pg_input_is_valid (PostgreSQL 16+) to keep the plugin working
        // on older servers, at the cost of a subtransaction per call.
        migrationBuilder.Sql("""
            CREATE FUNCTION json_valid(t text) RETURNS integer
            LANGUAGE plpgsql IMMUTABLE STRICT AS $$
            BEGIN
                PERFORM t::jsonb;
                RETURN 1;
            EXCEPTION WHEN others THEN
                RETURN 0;
            END $$;
            """);

        // Only the top level object keys this Jellyfin migration uses are supported. Paths arrive in
        // SQLite notation ('$.Key'), and ltrim strips the '$' and '.' characters to leave the key name.
        // SQLite's array element and nested paths are not implemented, and the rewritten document is
        // normalised by jsonb: key order and duplicate keys may change, which does not affect Jellyfin
        // since it deserializes this column rather than comparing its text.
        migrationBuilder.Sql("""
            CREATE FUNCTION json_remove(t text, VARIADIC paths text[]) RETURNS text
            LANGUAGE plpgsql IMMUTABLE STRICT AS $$
            DECLARE
                result jsonb := t::jsonb;
                path text;
            BEGIN
                FOREACH path IN ARRAY paths LOOP
                    result := result - ltrim(path, '$.');
                END LOOP;
                RETURN result::text;
            END $$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS json_valid(text);
            DROP FUNCTION IF EXISTS json_remove(text, text[]);
            """);
    }
}
