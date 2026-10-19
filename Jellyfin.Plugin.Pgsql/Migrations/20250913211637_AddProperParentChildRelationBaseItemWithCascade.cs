using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Plugin.Pgsql.Migrations
{
    /// <inheritdoc />
    public partial class AddProperParentChildRelationBaseItemWithCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete BaseItems whose ParentId points to a non-existent row before
            // adding the foreign key - the constraint would otherwise fail on the
            // orphaned rows left behind by older Jellyfin releases. Mirrors the
            // recursive cleanup in the upstream Jellyfin migration of the same name:
            // a single-level delete would leave the orphan's descendants behind,
            // because their parent still exists in the statement's snapshot.
            migrationBuilder.Sql("""
WITH RECURSIVE Orphan ("Id") AS (
    SELECT Child."Id"
    FROM "BaseItems" AS Child
    WHERE Child."ParentId" IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM "BaseItems" AS Parent WHERE Parent."Id" = Child."ParentId")
    UNION
    SELECT Descendant."Id"
    FROM "BaseItems" AS Descendant
    INNER JOIN Orphan ON Descendant."ParentId" = Orphan."Id"
)
DELETE FROM "BaseItems" WHERE "Id" IN (SELECT "Id" FROM Orphan);
""");

            migrationBuilder.AddForeignKey(
                name: "FK_BaseItems_BaseItems_ParentId",
                table: "BaseItems",
                column: "ParentId",
                principalTable: "BaseItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BaseItems_BaseItems_ParentId",
                table: "BaseItems");
        }
    }
}
