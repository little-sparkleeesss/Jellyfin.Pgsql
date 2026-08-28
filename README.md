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

You can use your existing Jellyfin compose file and change the image accordingly to: `ghcr.io/jpvenson/jellyfin.pgsql:10.11.11-1`.

You need to add the connection parameters as enviornment variables in your compose file:

```yaml

services:
  jellyfin:
    image: ghcr.io/jpvenson/jellyfin.pgsql:10.11.11-1
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
      # Optional settings bellow, uncomment if you want to connect using SSL
      # - POSTGRES_SSLMODE=Require
      # - POSTGRES_TRUSTSERVERCERTIFICATE=true
      # Optional: per-command timeout in seconds (default 30, 0 = no limit).
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
    <PluginAssembly>../../../Jellyfin.Plugin.Pgsql/bin/debug/net9.0/Jellyfin.Plugin.Pgsql.dll</PluginAssembly>
    <PluginName>PostgreSQL</PluginName>
    <ConnectionString>CONNECTION_STRING_TO_LOCAL_PGSQL_SERVER</ConnectionString>
  </CustomProviderOptions>
  <LockingBehavior>NoLock</LockingBehavior>
</DatabaseConfigurationOptions>

```

Launch your Jellyfin server.

> [!IMPORTANT]
> Manual (non-docker) installs additionally require:
> - The `pg_dump` and `psql` binaries on `PATH` - the plugin shells out to them for the automatic pre-migration backup and restore, and a missing binary aborts startup.
> - A PostgreSQL user with `CREATEDB` privilege - Jellyfin creates the database itself on first run (unless you pre-create it).
> - The `POSTGRES_*` environment variables described above. The `ConnectionString` in `database.xml` is honored (SSL mode, trust settings etc.), but the environment variables remain the fallback and the documentation baseline.

# Add migration
Run `dotnet ef migrations add {MIGRATION_NAME} --project "/workspaces/Jellyfin.Pgsql/Jellyfin.Plugin.Pgsql" -- --migration-provider Jellyfin-PgSql`

> [!NOTE]
> The `-- --migration-provider Jellyfin-PgSql` passthrough currently has no effect:
> the design-time factory (`PgSqlDesignTimeJellyfinDbFactory`) always selects
> Npgsql and ignores the arguments. It is kept for compatibility with future
> Jellyfin versions that may use it.

# Release flow

To create a new release, first sync all Jellyfin server changes, then add a new migration as seen above and build the container. No EF bundle is required: Jellyfin applies pending migrations from the plugin's migrations assembly through its own migration service at startup.

# Migration Instructions (ADVANCED, UNTESTED)

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
- **pgloader migration path** - the load file backfills `NormalizedUsername` and re-creates its unique index after loading. It fails loudly if case-variant duplicate usernames exist in the source database. The migration path remains experimental (UNTESTED).
