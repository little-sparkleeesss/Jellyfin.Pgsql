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
            // cleanup in the upstream Jellyfin migration of the same name.
            migrationBuilder.Sql("""
DELETE FROM "BaseItems"
        WHERE
        "ParentId" IS NOT NULL
        AND
        NOT EXISTS(SELECT 1 FROM "BaseItems" parent WHERE parent."Id" = "BaseItems"."ParentId");
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
