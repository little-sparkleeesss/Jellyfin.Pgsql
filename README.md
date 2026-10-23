# The Unofficial Postgre SQL adapter for Jellyfin Server

This plugin adds postgres SQL support to [Jellyfin Server](https://github.com/jellyfin/jellyfin).


> [!IMPORTANT]
> Pleae note that there are several additional steps required to make this work and it is to be considered __HIGHLY__ experimental.
> 
> This plugin is __NOT__ meant for a production jellyfin server. This is just for advanced users to experiment and evaluate potential issues we will face once we will include pgsql support in the jellyfin server by default.
> 
> Issues __MAY__ not be fixed by me and if i dont have the time to work on it stuff will not nessesarily be done.
> 
> # !!!Use at your own risk!!!
> Full explaination here: https://github.com/JPVenson/Jellyfin.Pgsql/issues/27

# How to use it

You can use your existing Jellyfin compose file and change the image accordingly to: `ghcr.io/jpvenson/jellyfin.pgsql:12.2-1`.

You need to add the connection parameters as enviornment variables in your compose file:

```yaml

services:
  jellyfin:
    image: ghcr.io/jpvenson/jellyfin.pgsql:12.2-1
    volumes:
        - /path/to/config:/config
        - /path/to/cache:/cache
        - /path/to/media:/media
    environment:
        - POSTGRES_HOST=
        - POSTGRES_PORT=
        - POSTGRES_DB=jellyfin
        - POSTGRES_USER=jellyfin
        - POSTGRES_PASSWORD=jellyfin
      # Optional: SSL via individual variables. For full verification
      # (verify CA + hostname) use POSTGRES_SSLMODE=VerifyFull and mount the
      # CA file into the container, e.g. - /path/to/ca.pem:/config/ca.pem:ro
      # - POSTGRES_SSLMODE=Require
      # - POSTGRES_ROOTCERTIFICATE=/config/ca.pem
      # Npgsql 10 ignores Trust Server Certificate. Use SSL Mode=Require to
      # encrypt without certificate validation (vulnerable to MITM), or
      # VerifyFull with a root certificate to validate the server.
      # - POSTGRES_TRUSTSERVERCERTIFICATE=true   (accepted, does nothing)
      # Optional: per-command timeout in seconds (default 30, 0 = no limit).
      # Applies to normal queries; EF migrations run without a command timeout
      # because the 12.1 conversion can exceed it on large libraries.
      # Raise it if large libraries hit query timeouts.
      # - POSTGRES_COMMAND_TIMEOUT=120
```

# Build

Checkout the Jellyfin submodule.
Use dotnet build to build the plugin.
Place the plugin in the `plugins` folder of the Jellyfin app.
Update the `database.xml` file to switch to the plugin as its database provider:

```xml
<?xml version="1.0" encoding="utf-8"?>
<DatabaseConfigurationOptions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <DatabaseType>PLUGIN_PROVIDER</DatabaseType>
  <CustomProviderOptions>
    <PluginAssembly>../../../Jellyfin.Plugin.Pgsql/bin/debug/net10.0/Jellyfin.Plugin.Pgsql.dll</PluginAssembly>
    <PluginName>PostgreSQL</PluginName>
    <ConnectionString></ConnectionString>
  </CustomProviderOptions>
  <LockingBehavior>NoLock</LockingBehavior>
</DatabaseConfigurationOptions>

```

Leave `ConnectionString` empty to use the `POSTGRES_*` environment variables. A non-empty value must be a valid Npgsql connection string; values it omits are filled from the environment variables and their defaults.

Launch your Jellyfin server.

> [!IMPORTANT]
> Manual (non-docker) installs additionally require:
> - The `pg_dump` and `psql` binaries (PostgreSQL client 17 or newer) on `PATH` - the plugin shells out to them for the automatic pre-migration backup and restore, and a missing binary aborts startup. The backup uses `pg_dump --exclude-extension=*` (PostgreSQL 17+), so an older client fails the backup with an unrecognized option.
> - A PostgreSQL user with `CREATEDB` privilege - Jellyfin creates the database itself on first run (unless you pre-create it).
> - The `POSTGRES_*` environment variables described above. The `ConnectionString` in `database.xml` is honored (SSL mode, trust settings etc.); values it omits fall back to these variables and their defaults. For a manual install, put SSL settings in the `ConnectionString` itself - the `POSTGRES_SSLMODE`/`POSTGRES_ROOTCERTIFICATE` variables are folded into it by the Docker entrypoint only.

# Add migration
Run `dotnet ef migrations add {MIGRATION_NAME} --project "/workspaces/Jellyfin.Pgsql/Jellyfin.Plugin.Pgsql" -- --migration-provider Jellyfin-PgSql`

> [!NOTE]
> The `-- --migration-provider Jellyfin-PgSql` passthrough currently has no effect:
> the design-time factory (`PgSqlDesignTimeJellyfinDbFactory`) always selects
> Npgsql and ignores the arguments. It is kept for compatibility with future
> Jellyfin versions that may use it.

# Release flow

To create a new release, first sync all Jellyfin server changes, then add a new migration as seen above and build the container. No EF bundle is required: Jellyfin applies pending migrations from the plugin's migrations assembly through its own migration service at startup.

# Migration Instructions (ADVANCED, CURRENTLY BROKEN)

> [!WARNING]
> The pgloader path below is verified broken against the 12.1 schema (see Known limitations). Do not follow it until `docker/jellyfindb.load` is reworked.

To migrate your existing Jellyfin instance to a custom database (not using the docker image) follow the steps IN THIS ORDER.

1. Download the Jellyfin PGSQL container and configure it to point to an existing empty database and empty config directory. DO NOT USE YOUR EXISTING DATA OR SQLITE LIBRARY CONFIGURE A FULLY CLEAR INSTANCE.
2. Run Jellyfin once with it configured to your empty database, this will seed the database and its migration history.
3. Stop your Jellyfin instance after it has been started once (no need to fully configure it via the setup wizard). If you did not get the setup wizard then you did something wrong!
4. Install the pgloader tool `apt install pgloader` or see https://pgloader.readthedocs.io/en/latest/install.html.
5. Download the [jellyfindb.load](/docker/jellyfindb.load) file
6. Adapt the `jellyfindb.load` file accordingly to point towards your old jellyfin.db and your postgres instance. See https://pgloader.readthedocs.io/en/latest/ref/sqlite.html
7. Use the load file in `jellyfindb.load` to transfer your sqlite db into the postgres db like `pgloader /jellyfin-pgsql/jellyfindb.load`.
8. Move your old Data back to the Jellyfin directories
9. Start Jellyfin

If you get an error regarding a missing `__EFMigrationsHistory` you did not start Jellyfin with a clear state.

# Known limitations

- **Case-sensitive `LIKE` search** - Jellyfin's `NameContains` filter translates to `LIKE`, which is case-insensitive for ASCII on SQLite but strictly case-sensitive on PostgreSQL. The main search box is unaffected (both sides are lowercased). Fixing this requires changing Jellyfin's query translation, not this plugin.
- **Collation migration locks large tables** - the `20260829120000_ApplyCCollation` migration rewrites every text column with `COLLATE "C"`. Fresh installs run it on empty tables; existing installs pay a one-time table rewrite under an `ACCESS EXCLUSIVE` lock - plan the upgrade window accordingly.
- **`MigrateRatingLevels` cannot run on PostgreSQL, so the plugin buffers its query** - the Jellyfin 12.1/12.2 routine enumerates `BaseItems.AsNoTracking().Select(e => e.OfficialRating).Distinct()` and calls `ExecuteUpdate` on the same `DbContext` from inside the loop. SQLite tolerates a second command while a reader is open, PostgreSQL does not, so the routine throws `NpgsqlOperationInProgressException` and startup aborts on any database that already has items. `MigrateRatingLevelsInterceptor` drains that one query into memory before handing the reader back, leaving the connection idle; the routine then runs normally in the same transaction and records itself as applied. The interception logs `Buffered N rows of the MigrateRatingLevels rating query`; if a startup aborts in this routine without that line, widen `IsRatingLevelsQuery`. Once upstream materialises the query, delete the interceptor and its registration - no database cleanup is needed.
- **pgloader migration path** - verified broken against the 12.1 schema for several reasons: the load file uses `${VAR}`, which pgloader does not parse (it offers `GETENV 'varname'`, Mustache `{{VAR}}` with `--context`, and the `PG*` environment variables when URI components are omitted); the SQLite source still has `BaseItems.ExtraIds`, `Permissions.Permission_Permissions_Guid` and `Preferences.Preference_Preferences_Guid`, which no longer exist in the target and which pgloader cannot exclude per column - all of its filters are table-level, and `CAST` can retype a column but not drop it from the COPY list; the `after load` step recreates `IX_Users_NormalizedUsername`, which the plugin's migrations already created; and `truncate` issues a plain `TRUNCATE` with no `CASCADE`, which PostgreSQL refuses on any table referenced by a foreign key - seventeen of them point at `BaseItems`, so the load cannot get past it. The path remains experimental (UNTESTED) until the load file is reworked.
- **Upgrading an existing 10.11 database can lose data** - Jellyfin 12.1 drops `BaseItems.ExtraIds`; the column and its contents are removed by the `20260919055826_Jellyfin12.1` migration and cannot be restored by rolling back. `OwnerId` and `PrimaryVersionId` are also rewritten from text to `uuid`; values that are not canonical GUIDs are cleared, and `OwnerId` values that point at a missing item are reattached to the placeholder item. Take a backup (`pg_dump`) before upgrading.
- **EF migrations ignore `POSTGRES_COMMAND_TIMEOUT`** - the 12.1 migration scans and rewrites the whole `BaseItems` table and builds its indexes, which exceeds the 30s default on a large library and aborts the migration (on a timed-out migration the plugin tries to restore the pre-migration backup). `PgSqlMigrationCommandExecutor` clears the command timeout while EF executes migration commands; normal queries keep the configured value.
- **Pre-migration backups omit extensions and restore atomically** - the dump is taken with `--exclude-extension=*` so it can be replayed by the Jellyfin role even when an administrator installed an extension (`pgaudit`, for example) that the role does not own; the dump therefore contains Jellyfin's schema and data only. The restore drops the Jellyfin tables and replays the dump in a single transaction: if the replay fails (an older backup still contains extension DDL, for instance), the wipe is rolled back and the database is left as it was.
- **Two SQLite compatibility functions are created in your database** - Jellyfin 12.1 ships the `StripEmbeddedLinkedChildren` startup routine, which runs raw SQLite SQL (`json_remove`, `json_valid`) against whichever provider is configured. PostgreSQL has neither function, and a failing routine aborts startup, so the plugin's migration defines them in the current schema. `json_remove` only understands the top level `'$.Key'` paths that routine passes, and rewriting `BaseItems.Data` through `jsonb` normalises key order and whitespace (semantics are unaffected - the server reads it with `System.Text.Json`).
- **First start after the upgrade runs an untimed full `VACUUM ANALYZE`** - Jellyfin 12.1 calls `RunScheduledOptimisation` after migrations have modified the database, not only on its schedule. On a large library expect a long first start; the startup UI shows `Optimizing database` while it runs.
- **Jellyfin 12.2 refreshes planner statistics after every library scan** - the new `RefreshStatistics` provider call is implemented as `ANALYZE`, which the interface asks to stay cheap enough to run per scan (unlike `RunScheduledOptimisation`, it must not reclaim space). Like the scheduled optimisation it lifts the command timeout, because `ANALYZE` on a large library exceeds the 30s default.
