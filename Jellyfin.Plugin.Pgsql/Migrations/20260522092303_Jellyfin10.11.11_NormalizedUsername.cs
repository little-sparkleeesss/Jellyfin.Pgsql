using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Plugin.Pgsql.Migrations;

/// <inheritdoc />
public partial class Jellyfin101111NormalizedUsername : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NormalizedUsername",
            table: "Users",
            type: "character varying(255)",
            maxLength: 255,
            nullable: false,
            defaultValue: string.Empty);

        // Backfill existing rows so the unique index added by the next
        // migration is created on unique, non-empty values. The Jellyfin code
        // migration 20260522092304_UpdateNormalizedUsername performs the same
        // backfill, but only for installs where the code migration actually
        // runs - this guards the unique index creation itself.
        migrationBuilder.Sql("""
UPDATE "Users"
SET "NormalizedUsername" = UPPER("Username")
WHERE "NormalizedUsername" = '';
""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "NormalizedUsername",
            table: "Users");
    }
}
