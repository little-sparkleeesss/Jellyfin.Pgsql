using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Plugin.Pgsql.Migrations
{
    /// <inheritdoc />
    public partial class Jellyfin121 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data fixes that have to run while the affected columns still have their old
            // shape. They mirror the SQLite provider migrations 20260113203012_ChangeOwnerIdToGuid,
            // 20260215201634_ChangePrimaryVersionIdToGuid and
            // 20260815063607_RemoveOrphanedUserPermissionsAndPreferences, which EF cannot
            // scaffold from a model diff.

            // OwnerId and PrimaryVersionId become uuid further down. PostgreSQL refuses that cast
            // unless every existing value already parses as a GUID, so normalise first: expand the
            // unhyphenated 32 character form, upper-case, then drop anything that is not a
            // canonical GUID (including the all-zero placeholder). The regex replaces upstream's
            // "length != 36" check, which would still let a 36 character non-GUID through.
            migrationBuilder.Sql("""
                UPDATE "BaseItems" SET "OwnerId" = UPPER("OwnerId")
                WHERE "OwnerId" IS NOT NULL AND "OwnerId" <> UPPER("OwnerId");

                UPDATE "BaseItems" SET "OwnerId" = NULL
                WHERE "OwnerId" IS NOT NULL
                  AND "OwnerId" !~ '^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$';

                UPDATE "BaseItems" SET "OwnerId" = NULL
                WHERE "OwnerId" IS NOT NULL AND "OwnerId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "BaseItems" SET "PrimaryVersionId" = UPPER(
                    SUBSTR("PrimaryVersionId",1,8)||'-'||SUBSTR("PrimaryVersionId",9,4)||'-'||
                    SUBSTR("PrimaryVersionId",13,4)||'-'||SUBSTR("PrimaryVersionId",17,4)||'-'||
                    SUBSTR("PrimaryVersionId",21,12))
                WHERE "PrimaryVersionId" IS NOT NULL AND LENGTH("PrimaryVersionId") = 32;

                UPDATE "BaseItems" SET "PrimaryVersionId" = UPPER("PrimaryVersionId")
                WHERE "PrimaryVersionId" IS NOT NULL AND "PrimaryVersionId" <> UPPER("PrimaryVersionId");

                UPDATE "BaseItems" SET "PrimaryVersionId" = NULL
                WHERE "PrimaryVersionId" IS NOT NULL
                  AND "PrimaryVersionId" !~ '^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$';

                UPDATE "BaseItems" SET "PrimaryVersionId" = NULL
                WHERE "PrimaryVersionId" IS NOT NULL AND "PrimaryVersionId" = '00000000-0000-0000-0000-000000000000';
                """);

            // Preferences.UserId and Permissions.UserId become NOT NULL further down, so the
            // orphaned rows upstream removes have to go first.
            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "UserId" IS NULL OR "UserId" NOT IN (SELECT "Id" FROM "Users");
                DELETE FROM "Preferences" WHERE "UserId" IS NULL OR "UserId" NOT IN (SELECT "Id" FROM "Users");
                """);

            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_Preferences_UserId_Kind",
                table: "Preferences");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_UserId_Kind",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_PeopleBaseItemMap_PeopleId",
                table: "PeopleBaseItemMap");

            migrationBuilder.DropIndex(
                name: "IX_MediaStreamInfos_StreamIndex",
                table: "MediaStreamInfos");

            migrationBuilder.DropIndex(
                name: "IX_MediaStreamInfos_StreamIndex_StreamType",
                table: "MediaStreamInfos");

            migrationBuilder.DropIndex(
                name: "IX_MediaStreamInfos_StreamIndex_StreamType_Language",
                table: "MediaStreamInfos");

            migrationBuilder.DropIndex(
                name: "IX_MediaStreamInfos_StreamType",
                table: "MediaStreamInfos");

            migrationBuilder.DropIndex(
                name: "IX_Devices_DeviceId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_Id_Type_IsFolder_IsVirtualItem",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItemProviders_ProviderId_ProviderValue_ItemId",
                table: "BaseItemProviders");

            migrationBuilder.DropIndex(
                name: "IX_BaseItemImageInfos_ItemId",
                table: "BaseItemImageInfos");

            migrationBuilder.DropColumn(
                name: "Preference_Preferences_Guid",
                table: "Preferences");

            migrationBuilder.DropColumn(
                name: "Permission_Permissions_Guid",
                table: "Permissions");

            // EF matched the removed ExtraIds column against the newly added OriginalLanguage
            // column and scaffolded a rename, which would move the alternate-version ids into the
            // new column. Upstream drops ExtraIds outright (20260113233500_DropExtraIdsColumn) and
            // adds OriginalLanguage as an empty column (20260113233000_AddForeignKeyToOwnerId).
            migrationBuilder.DropColumn(
                name: "ExtraIds",
                table: "BaseItems");

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "BaseItems",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Preferences",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Permissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // defaultValue exists only to let EF make the columns NOT NULL: it emits
            // UPDATE ... WHERE "UserId" IS NULL and SET DEFAULT. The model declares no
            // default, so drop it again; otherwise an insert that omits UserId silently
            // stores the empty GUID instead of failing.
            migrationBuilder.Sql("""
                ALTER TABLE "Preferences" ALTER COLUMN "UserId" DROP DEFAULT;
                ALTER TABLE "Permissions" ALTER COLUMN "UserId" DROP DEFAULT;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsOriginal",
                table: "MediaStreamInfos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // EF scaffolds "ALTER COLUMN ... TYPE uuid" without a USING clause, which PostgreSQL
            // rejects: the text -> uuid input conversion is explicit-only, there is no assignment
            // cast. Both columns were normalised to canonical GUID text at the top of this
            // migration, so the cast cannot fail; the empty string is mapped to NULL as a last
            // line of defence.
            migrationBuilder.Sql("""
                ALTER TABLE "BaseItems" ALTER COLUMN "PrimaryVersionId" TYPE uuid USING NULLIF("PrimaryVersionId", '')::uuid;
                ALTER TABLE "BaseItems" ALTER COLUMN "OwnerId" TYPE uuid USING NULLIF("OwnerId", '')::uuid;
                """);

            migrationBuilder.CreateTable(
                name: "LinkedChildren",
                columns: table => new
                {
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedChildren", x => new { x.ParentId, x.SortOrder });
                    table.ForeignKey(
                        name: "FK_LinkedChildren_BaseItems_ChildId",
                        column: x => x.ChildId,
                        principalTable: "BaseItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LinkedChildren_BaseItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "BaseItems",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "BaseItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "Name", "OwnerId", "PrimaryVersionId" },
                values: new object[] { "This is a placeholder item for UserData that has been detached from its original item", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId_IsFavorite_ItemId",
                table: "UserData",
                columns: new[] { "UserId", "IsFavorite", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId_ItemId_LastPlayedDate",
                table: "UserData",
                columns: new[] { "UserId", "ItemId", "LastPlayedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId_Played_ItemId",
                table: "UserData",
                columns: new[] { "UserId", "Played", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Preferences_UserId_Kind",
                table: "Preferences",
                columns: new[] { "UserId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_UserId_Kind",
                table: "Permissions",
                columns: new[] { "UserId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeopleBaseItemMap_PeopleId_ItemId",
                table: "PeopleBaseItemMap",
                columns: new[] { "PeopleId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaStreamInfos_StreamType_ItemId_Language_IsExternal",
                table: "MediaStreamInfos",
                columns: new[] { "StreamType", "ItemId", "Language", "IsExternal" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_ExtraType_OwnerId",
                table: "BaseItems",
                columns: new[] { "ExtraType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_Name",
                table: "BaseItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_OwnerId",
                table: "BaseItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_PrimaryVersionId",
                table: "BaseItems",
                column: "PrimaryVersionId",
                filter: "\"PrimaryVersionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_SeasonId",
                table: "BaseItems",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_SeriesId",
                table: "BaseItems",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_SeriesName",
                table: "BaseItems",
                column: "SeriesName");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_TopParentId_IsFolder_IsVirtualItem_DateCreated",
                table: "BaseItems",
                columns: new[] { "TopParentId", "IsFolder", "IsVirtualItem", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_TopParentId_MediaType_IsVirtualItem_DateCreated",
                table: "BaseItems",
                columns: new[] { "TopParentId", "MediaType", "IsVirtualItem", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_TopParentId_Type_IsVirtualItem",
                table: "BaseItems",
                columns: new[] { "TopParentId", "Type", "IsVirtualItem" },
                filter: "\"PrimaryVersionId\" IS NULL AND (\"OwnerId\" IS NULL OR \"ExtraType\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_TopParentId_Type_IsVirtualItem_DateCreated",
                table: "BaseItems",
                columns: new[] { "TopParentId", "Type", "IsVirtualItem", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_Type_CleanName",
                table: "BaseItems",
                columns: new[] { "Type", "CleanName" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_Type_SeriesPresentationUniqueKey_ParentIndexNumbe~",
                table: "BaseItems",
                columns: new[] { "Type", "SeriesPresentationUniqueKey", "ParentIndexNumber", "IndexNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_Type_TopParentId_SortName",
                table: "BaseItems",
                columns: new[] { "Type", "TopParentId", "SortName" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItemProviders_ProviderId_ItemId_ProviderValue",
                table: "BaseItemProviders",
                columns: new[] { "ProviderId", "ItemId", "ProviderValue" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItemImageInfos_ItemId_ImageType",
                table: "BaseItemImageInfos",
                columns: new[] { "ItemId", "ImageType" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedChildren_ChildId_ChildType",
                table: "LinkedChildren",
                columns: new[] { "ChildId", "ChildType" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedChildren_ParentId_ChildType",
                table: "LinkedChildren",
                columns: new[] { "ParentId", "ChildType" });

            // FK_BaseItems_BaseItems_OwnerId would abort on an OwnerId that does not resolve to a
            // row. Upstream (20260113233000_AddForeignKeyToOwnerId) repoints those at the
            // placeholder item 00000000-0000-0000-0000-000000000001. The EXISTS guard makes the
            // foreign key unfailable on a database where that placeholder has not been created
            // yet; otherwise the value is simply cleared.
            migrationBuilder.Sql("""
                UPDATE "BaseItems" SET "OwnerId" = '00000000-0000-0000-0000-000000000001'
                WHERE "OwnerId" IS NOT NULL
                  AND "OwnerId" NOT IN (SELECT "Id" FROM "BaseItems")
                  AND EXISTS (SELECT 1 FROM "BaseItems" WHERE "Id" = '00000000-0000-0000-0000-000000000001');

                UPDATE "BaseItems" SET "OwnerId" = NULL
                WHERE "OwnerId" IS NOT NULL
                  AND "OwnerId" NOT IN (SELECT "Id" FROM "BaseItems");
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_BaseItems_BaseItems_OwnerId",
                table: "BaseItems",
                column: "OwnerId",
                principalTable: "BaseItems",
                principalColumn: "Id");

            // Expression index, so it cannot be declared on the entity type and EF's model differ
            // has no way to see it. Mirrors upstream 20260728170000_AddPeopleNameLowerIndex:
            // /Persons collapses the one-row-per-(Name, PersonType) table to one row per
            // lowercased name, and without this index that dedup scans and groups the whole table
            // on every request.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Peoples_NameLower" ON "Peoples" (lower("Name"));
                """);

            // Jellyfin 12.1 adds the StripEmbeddedLinkedChildren routine, which runs against
            // whatever provider is configured and executes raw SQLite SQL using json_remove and
            // json_valid. PostgreSQL has neither function (verified against the PostgreSQL 18
            // JSON reference), and a failing routine aborts startup, so provide SQLite compatible
            // shims. The routine is an AppInitialisation stage migration while EF migrations run
            // in CoreInitialisation, so these exist before it executes.
            // Caveats to revisit on every Jellyfin upgrade: json_remove only understands the top
            // level '$.Key' paths that routine currently passes, and the jsonb round trip
            // normalises key order and whitespace inside BaseItems.Data.
            // Validate with jsonb, the same parser json_remove uses: json accepts escapes such as
            // \u0000 that jsonb rejects, which would pass this guard and then make json_remove
            // throw, aborting startup.
            // json_valid returns integer rather than boolean because the routine compares it to 1.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION json_valid(txt text) RETURNS integer
                LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE AS $$
                BEGIN
                    PERFORM txt::jsonb;
                    RETURN 1;
                EXCEPTION WHEN others THEN
                    RETURN 0;
                END $$;

                CREATE OR REPLACE FUNCTION json_remove(txt text, VARIADIC paths text[]) RETURNS text
                LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE AS $$
                DECLARE
                    doc jsonb;
                    p   text;
                BEGIN
                    IF txt IS NULL THEN
                        RETURN NULL;
                    END IF;

                    doc := txt::jsonb;
                    IF paths IS NOT NULL THEN
                        FOREACH p IN ARRAY paths LOOP
                            doc := doc - ltrim(p, '$.');
                        END LOOP;
                    END IF;
                    RETURN doc::text;
                END $$;
                """);

            // C collation for the text columns this migration adds or retypes, matching the
            // column-wise approach of 20260829120000_ApplyCCollation. Querying information_schema
            // keeps it idempotent (already "C" columns are skipped) and avoids another
            // hand-maintained list of every column.
            // ALTER COLUMN ... TYPE rewrites the table under an ACCESS EXCLUSIVE lock, so on an
            // upgrade this touches only the genuinely new columns; on a fresh database the tables
            // are empty.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    r record;
                BEGIN
                    FOR r IN
                        SELECT c.table_name, c.column_name, c.data_type, c.character_maximum_length
                          FROM information_schema.columns c
                          JOIN information_schema.tables t
                            ON t.table_schema = c.table_schema AND t.table_name = c.table_name
                         WHERE c.table_schema = current_schema()
                           AND t.table_type = 'BASE TABLE'
                           AND c.table_name <> '__EFMigrationsHistory'
                           AND c.data_type IN ('text', 'character varying', 'character')
                           AND c.collation_name IS DISTINCT FROM 'C'
                    LOOP
                        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE %s COLLATE "C"',
                            r.table_name,
                            r.column_name,
                            CASE
                                WHEN r.data_type = 'text' THEN 'text'
                                WHEN r.character_maximum_length IS NOT NULL
                                    THEN format('character varying(%s)', r.character_maximum_length)
                                ELSE 'character varying'
                            END);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BaseItems_BaseItems_OwnerId",
                table: "BaseItems");

            migrationBuilder.DropTable(
                name: "LinkedChildren");

            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId_IsFavorite_ItemId",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId_ItemId_LastPlayedDate",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId_Played_ItemId",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_Preferences_UserId_Kind",
                table: "Preferences");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_UserId_Kind",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_PeopleBaseItemMap_PeopleId_ItemId",
                table: "PeopleBaseItemMap");

            migrationBuilder.DropIndex(
                name: "IX_MediaStreamInfos_StreamType_ItemId_Language_IsExternal",
                table: "MediaStreamInfos");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_ExtraType_OwnerId",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_Name",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_OwnerId",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_PrimaryVersionId",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_SeasonId",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_SeriesId",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_SeriesName",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_TopParentId_IsFolder_IsVirtualItem_DateCreated",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_TopParentId_MediaType_IsVirtualItem_DateCreated",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_TopParentId_Type_IsVirtualItem",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_TopParentId_Type_IsVirtualItem_DateCreated",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_Type_CleanName",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_Type_SeriesPresentationUniqueKey_ParentIndexNumbe~",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_Type_TopParentId_SortName",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItemProviders_ProviderId_ItemId_ProviderValue",
                table: "BaseItemProviders");

            migrationBuilder.DropIndex(
                name: "IX_BaseItemImageInfos_ItemId_ImageType",
                table: "BaseItemImageInfos");

            migrationBuilder.DropColumn(
                name: "IsOriginal",
                table: "MediaStreamInfos");

            // The counterpart of the drop/add pair in Up. The original ExtraIds contents are gone,
            // so a rollback can restore the column but not its values.
            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "BaseItems");

            migrationBuilder.AddColumn<string>(
                name: "ExtraIds",
                table: "BaseItems",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Peoples_NameLower";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Preferences",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "Preference_Preferences_Guid",
                table: "Preferences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Permissions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "Permission_Permissions_Guid",
                table: "Permissions",
                type: "uuid",
                nullable: true);

            // Same reasoning as the Up direction: be explicit rather than relying on the
            // uuid -> text conversion being assignment castable.
            // The json_valid/json_remove shims are deliberately left in place: they are inert
            // unless Jellyfin 12.x calls them, and dropping functions while a server may be
            // running is riskier than leaving them behind.
            migrationBuilder.Sql("""
                ALTER TABLE "BaseItems" ALTER COLUMN "PrimaryVersionId" TYPE text USING "PrimaryVersionId"::text;
                ALTER TABLE "BaseItems" ALTER COLUMN "OwnerId" TYPE text USING "OwnerId"::text;
                """);

            // Written as SQL rather than migrationBuilder.UpdateData: EF cannot resolve the column types
            // for this data operation in the downgrade direction and refuses to script it ("no entity
            // type mapped to the table 'BaseItems'"), which breaks `migrations script` and
            // `database update` rollbacks. All it reverts is the placeholder row's display name,
            // which upstream corrects from "detacted" to "detached".
            migrationBuilder.Sql("""
                UPDATE "BaseItems"
                SET "Name" = 'This is a placeholder item for UserData that has been detacted from its original item'
                WHERE "Id" = '00000000-0000-0000-0000-000000000001';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId",
                table: "UserData",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Preferences_UserId_Kind",
                table: "Preferences",
                columns: new[] { "UserId", "Kind" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_UserId_Kind",
                table: "Permissions",
                columns: new[] { "UserId", "Kind" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PeopleBaseItemMap_PeopleId",
                table: "PeopleBaseItemMap",
                column: "PeopleId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaStreamInfos_StreamIndex",
                table: "MediaStreamInfos",
                column: "StreamIndex");

            migrationBuilder.CreateIndex(
                name: "IX_MediaStreamInfos_StreamIndex_StreamType",
                table: "MediaStreamInfos",
                columns: new[] { "StreamIndex", "StreamType" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaStreamInfos_StreamIndex_StreamType_Language",
                table: "MediaStreamInfos",
                columns: new[] { "StreamIndex", "StreamType", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaStreamInfos_StreamType",
                table: "MediaStreamInfos",
                column: "StreamType");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceId",
                table: "Devices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_Id_Type_IsFolder_IsVirtualItem",
                table: "BaseItems",
                columns: new[] { "Id", "Type", "IsFolder", "IsVirtualItem" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItemProviders_ProviderId_ProviderValue_ItemId",
                table: "BaseItemProviders",
                columns: new[] { "ProviderId", "ProviderValue", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItemImageInfos_ItemId",
                table: "BaseItemImageInfos",
                column: "ItemId");
        }
    }
}
