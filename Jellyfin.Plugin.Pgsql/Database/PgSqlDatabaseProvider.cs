using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.DbConfiguration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Jellyfin.Plugin.Pgsql.Database;

/// <summary>
/// Configures jellyfin to use an Postgres database.
/// </summary>
[JellyfinDatabaseProviderKey("Jellyfin-PgSql")]
public sealed class PgSqlDatabaseProvider : IJellyfinDatabaseProvider
{
    private const string BackupFolderName = "PgsqlBackups";

    // Drop the tables in the schema that holds Jellyfin's migration history before
    // replaying the dump. Dropping the schema itself requires owning it, which is not
    // guaranteed when the database was pre-created (the README allows that); dropping
    // the tables with CASCADE removes their dependants and only needs table ownership.
    private const string WipeSql = """
        DO $wipe$
        DECLARE
            target text;
            tbl record;
        BEGIN
            SELECT schemaname INTO target
              FROM pg_tables
             WHERE tablename = '__EFMigrationsHistory'
             ORDER BY (schemaname = current_schema()) DESC
             LIMIT 1;

            target := coalesce(target, current_schema()::text, 'public');

            FOR tbl IN SELECT tablename FROM pg_tables WHERE schemaname = target LOOP
                EXECUTE format('DROP TABLE IF EXISTS %I.%I CASCADE', target, tbl.tablename);
            END LOOP;
        END
        $wipe$;
        """;

    private readonly ILogger<PgSqlDatabaseProvider> _logger;
    private readonly IApplicationPaths _applicationPaths;
    private NpgsqlConnectionStringBuilder? _connectionBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlDatabaseProvider"/> class.
    /// </summary>
    /// <param name="applicationPaths">Service to construct the backup paths.</param>
    /// <param name="logger">A logger.</param>
    public PgSqlDatabaseProvider(IApplicationPaths applicationPaths, ILogger<PgSqlDatabaseProvider> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IDbContextFactory<JellyfinDbContext>? DbContextFactory { get; set; }

    /// <inheritdoc/>
    public void Initialise(DbContextOptionsBuilder options, DatabaseConfigurationOptions databaseConfiguration)
    {
        var customOptions = databaseConfiguration.CustomProviderOptions?.Options;

        // Prefer the connection string from database.xml, which is Jellyfin's
        // official configuration channel for custom providers (SSL mode, trust
        // settings etc. are honored). Any values it leaves out fall back to the
        // POSTGRES_* environment variables for the docker entrypoint flow.
        var connectionBuilder = ResolveConnectionBuilder(databaseConfiguration);
        ApplyCustomOptions(connectionBuilder, customOptions);
        connectionBuilder.ApplicationName = $"jellyfin+{FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion}";

        options
            .UseNpgsql(connectionBuilder.ToString(), pgSqlOptions =>
            {
                pgSqlOptions.MigrationsAssembly(GetType().Assembly.FullName);
            });

        // Jellyfin's MigrateRatingLevels routine enumerates a reader while writing through the same
        // connection, which PostgreSQL rejects. The interceptor buffers that one query so the
        // routine can run; re-check it on every Jellyfin upgrade, see the class remarks for how.
        options.AddInterceptors(new MigrateRatingLevelsInterceptor(_logger));

        // Migrations run through this same context, so they inherit the runtime command timeout.
        // The 12.1 migration scans and rewrites BaseItems and builds its indexes, which exceeds
        // the 30s default on large libraries; the timeout aborts the migration and triggers a
        // rollback, so migrations must run without it. Regular queries keep POSTGRES_COMMAND_TIMEOUT.
        options.ReplaceService<IMigrationCommandExecutor, PgSqlMigrationCommandExecutor>();

        // Cache the resolved builder so backup/restore tooling (pg_dump/psql)
        // talks to the exact same database as the runtime connection.
        _connectionBuilder = connectionBuilder;

        // Log the full connection string without password
        var safeConnectionString = new NpgsqlConnectionStringBuilder(connectionBuilder.ToString())
        {
            Password = null
        }.ToString();

        _logger.LogInformation("PostgreSQL connection string: {ConnectionString}", safeConnectionString);

        var enableSensitiveDataLogging = GetCustomDatabaseOption(customOptions, "EnableSensitiveDataLogging", e => e.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase), () => false);
        if (enableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging(enableSensitiveDataLogging);
            _logger.LogInformation("EnableSensitiveDataLogging is enabled on PostgreSQL connection");
        }
    }

    /// <inheritdoc/>
    public async Task RunScheduledOptimisation(CancellationToken cancellationToken)
    {
        if (DbContextFactory is null)
        {
            return;
        }

        var context = await DbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            if (context.Database.IsNpgsql())
            {
                // VACUUM ANALYZE can safely be interrupted and routinely exceeds the
                // 30s default command timeout on large libraries - lift the timeout.
                context.Database.SetCommandTimeout(0);
                await context.Database.ExecuteSqlRawAsync("VACUUM ANALYZE", cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("PostgreSQL database optimized successfully");
            }
        }
    }

    /// <inheritdoc/>
    public async Task RefreshStatistics(CancellationToken cancellationToken)
    {
        if (DbContextFactory is null)
        {
            return;
        }

        var context = await DbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            if (context.Database.IsNpgsql())
            {
                // ANALYZE rather than VACUUM ANALYZE: the interface asks for something cheap enough
                // to run after every library scan, so this must not reclaim space. It still
                // routinely exceeds the 30s default command timeout on large libraries - lift it.
                context.Database.SetCommandTimeout(0);
                await context.Database.ExecuteSqlRawAsync("ANALYZE", cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("PostgreSQL planner statistics refreshed successfully");
            }
        }
    }

    /// <inheritdoc/>
    public void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The C collation is applied column-wise by the ApplyCCollation migration.
        // It must NOT be set as a model-level annotation: NpgsqlDatabaseCreator
        // then emits CREATE DATABASE ... COLLATE "C", which PostgreSQL rejects
        // because it differs from the template database's collation (unless the
        // database is created from template0), breaking first-run database creation.

        // Configure all DateTime properties to ensure UTC for PostgreSQL compatibility, matching SQLite provider
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(
                        new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                            v => v.ToUniversalTime(),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
            }
        }
    }

    /// <inheritdoc/>
    public Task RunShutdownTask(CancellationToken cancellationToken)
    {
        // Clear Npgsql connection pools on shutdown
        NpgsqlConnection.ClearAllPools();
        _logger.LogInformation("PostgreSQL connection pools cleared on shutdown");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
    }

    /// <inheritdoc/>
    public async Task<string> MigrationBackupFast(CancellationToken cancellationToken)
    {
        // Milliseconds in the key so two backups started within the same second
        // do not silently overwrite each other.
        var key = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var backupFolder = Path.Combine(_applicationPaths.DataPath, BackupFolderName);
        Directory.CreateDirectory(backupFolder);

        var connectionBuilder = GetRuntimeConnectionBuilder();
        var backupFile = Path.Combine(backupFolder, $"{key}_{connectionBuilder.Database}.sql");

        // Extensions are database infrastructure, not part of Jellyfin's schema. An
        // administrator-installed extension (pgaudit, for example) is usually not owned by
        // the Jellyfin role, so replaying --clean's "DROP EXTENSION" fails and aborts the
        // restore. Leaving extensions out of the dump keeps it restorable by the Jellyfin
        // role and leaves the operator's extensions untouched. Requires pg_dump 17+; the
        // Docker image installs the client 18.
        var startInfo = new ProcessStartInfo
        {
            FileName = "pg_dump",
            Arguments = $"--host={connectionBuilder.Host} --port={connectionBuilder.Port} --username={connectionBuilder.Username} --dbname={connectionBuilder.Database} --file=\"{backupFile}\" --no-password --verbose --clean --if-exists --exclude-extension=*",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        ApplyPgEnvironment(startInfo, connectionBuilder);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        _logger.LogInformation("Starting PostgreSQL backup: {BackupFile}", backupFile);

        process.Start();

        // Read both pipes while pg_dump runs. Its --verbose output goes to stderr,
        // and leaving the redirected pipe unread will deadlock once it fills
        // (JPVenson/Jellyfin.Pgsql#39).
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Do not leave pg_dump running detached with its output pipes undrained.
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            throw;
        }

        var error = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("pg_dump failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new InvalidOperationException($"pg_dump failed: {error}");
        }

        _logger.LogInformation("PostgreSQL backup completed successfully: {BackupFile}", backupFile);
        return key;
    }

    /// <inheritdoc/>
    public async Task RestoreBackupFast(string key, CancellationToken cancellationToken)
    {
        NpgsqlConnection.ClearAllPools();

        var connectionBuilder = GetRuntimeConnectionBuilder();
        var backupFile = Path.Combine(_applicationPaths.DataPath, BackupFolderName, $"{key}_{connectionBuilder.Database}.sql");

        if (!File.Exists(backupFile))
        {
            _logger.LogCritical("Tried to restore a backup that does not exist: {Key}", key);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            // The dump is replayed with --clean --if-exists, but that alone cannot roll
            // back a migration that changed the schema: objects created after the backup
            // still depend on objects the dump wants to drop, so psql aborts part way
            // through and leaves the database in a mix of both schemas. Worse, those newer
            // objects would survive the restore while the restored migration history says
            // the migration never ran, so the next start would fail creating them again.
            // Wiping Jellyfin's tables first makes the replay land on an empty database,
            // which is what the backup actually describes. Anything the operator created
            // in that schema outside Jellyfin is dropped with it - the database belongs to
            // Jellyfin, and this path only runs when a migration already failed.
            // The wipe and the replay run in one transaction: if the dump cannot be
            // replayed (an old backup still contains extension DDL, for example), the wipe
            // is rolled back as well, so a failed restore leaves the database as it was
            // instead of dropping every table and stopping at the first error.
            FileName = "psql",
            Arguments = $"--host={connectionBuilder.Host} --port={connectionBuilder.Port} --username={connectionBuilder.Username} --dbname={connectionBuilder.Database} --no-password --quiet --single-transaction -v ON_ERROR_STOP=1 -c \"{WipeSql}\" --file=\"{backupFile}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        ApplyPgEnvironment(startInfo, connectionBuilder);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        _logger.LogInformation("Starting PostgreSQL restore from: {BackupFile}", backupFile);

        process.Start();

        // Same reason as MigrationBackupFast: read the pipes while psql runs so a
        // large restore can't fill the redirected output buffer and deadlock.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Do not leave psql running detached with its output pipes undrained.
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            throw;
        }

        var error = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("psql restore failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new InvalidOperationException($"psql restore failed: {error}");
        }

        _logger.LogInformation("PostgreSQL restore completed successfully from: {BackupFile}", backupFile);
    }

    /// <inheritdoc/>
    public Task DeleteBackup(string key)
    {
        var connectionBuilder = GetRuntimeConnectionBuilder();
        var backupFile = Path.Combine(_applicationPaths.DataPath, BackupFolderName, $"{key}_{connectionBuilder.Database}.sql");

        if (!File.Exists(backupFile))
        {
            _logger.LogCritical("Tried to delete a backup that does not exist: {Key}", key);
            return Task.CompletedTask;
        }

        File.Delete(backupFile);
        _logger.LogInformation("Deleted backup file: {BackupFile}", backupFile);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task PurgeDatabase(JellyfinDbContext dbContext, IEnumerable<string>? tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);

        var truncateQueries = new List<string>();
        foreach (var tableName in tableNames)
        {
            truncateQueries.Add($"TRUNCATE TABLE \"{tableName}\" RESTART IDENTITY CASCADE;");
        }

        var truncateAllQuery = string.Join('\n', truncateQueries);

        await dbContext.Database.ExecuteSqlRawAsync(truncateAllQuery).ConfigureAwait(false);
        _logger.LogInformation("PostgreSQL database tables purged successfully");
    }

    private T? GetCustomDatabaseOption<T>(ICollection<CustomDatabaseOption>? options, string key, Func<string, T> converter, Func<T>? defaultValue = null)
    {
        if (options is null)
        {
            return defaultValue is not null ? defaultValue() : default;
        }

        var value = options.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (value is null)
        {
            return defaultValue is not null ? defaultValue() : default;
        }

        return converter(value.Value);
    }

    private NpgsqlConnectionStringBuilder ResolveConnectionBuilder(DatabaseConfigurationOptions databaseConfiguration)
    {
        var connectionString = databaseConfiguration.CustomProviderOptions?.ConnectionString;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            NpgsqlConnectionStringBuilder builder;
            try
            {
                builder = new NpgsqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    "The ConnectionString in database.xml is not a valid Npgsql connection string. Leave it empty to use the POSTGRES_* environment variables.",
                    ex);
            }

            ApplyEnvironmentFallbacks(builder);
            return builder;
        }

        return GetConnectionBuilder();
    }

    /// <summary>
    /// Fills the values a database.xml connection string leaves out from the POSTGRES_*
    /// environment variables. Explicit connection string values take precedence, but the
    /// defaults (and POSTGRES_COMMAND_TIMEOUT) still apply when the string only sets the
    /// required values.
    /// </summary>
    /// <param name="connectionBuilder">The connection string parsed from database.xml.</param>
    private static void ApplyEnvironmentFallbacks(NpgsqlConnectionStringBuilder connectionBuilder)
    {
        if (!connectionBuilder.ShouldSerialize("Host"))
        {
            connectionBuilder.Host = GetEnvironmentValue("POSTGRES_HOST", "jellyfin");
        }

        if (!connectionBuilder.ShouldSerialize("Port"))
        {
            connectionBuilder.Port = int.Parse(GetEnvironmentValue("POSTGRES_PORT", "5432"), CultureInfo.InvariantCulture);
        }

        if (!connectionBuilder.ShouldSerialize("Database"))
        {
            connectionBuilder.Database = GetEnvironmentValue("POSTGRES_DB", "jellyfin");
        }

        if (!connectionBuilder.ShouldSerialize("Username"))
        {
            connectionBuilder.Username = GetEnvironmentValue("POSTGRES_USER", "jellyfin");
        }

        if (!connectionBuilder.ShouldSerialize("Password"))
        {
            var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("PostgreSQL password must be provided via POSTGRES_PASSWORD environment variable or the ConnectionString in database.xml");
            }

            connectionBuilder.Password = password;
        }

        if (!connectionBuilder.ShouldSerialize("Command Timeout"))
        {
            connectionBuilder.CommandTimeout = int.Parse(GetEnvironmentValue("POSTGRES_COMMAND_TIMEOUT", "30"), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Reads an environment variable, treating an empty value as unset. Docker Compose
    /// examples commonly leave optional variables such as POSTGRES_PORT empty.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="defaultValue">The value to use when the variable is missing or empty.</param>
    /// <returns>The environment value or the default.</returns>
    private static string GetEnvironmentValue(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private void ApplyCustomOptions(NpgsqlConnectionStringBuilder connectionBuilder, ICollection<CustomDatabaseOption>? options)
    {
        var includeErrorDetail = GetCustomDatabaseOption(options, "IncludeErrorDetail", e => e.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase), () => false);
        if (includeErrorDetail)
        {
            connectionBuilder.IncludeErrorDetail = includeErrorDetail;
        }

        var logParameters = GetCustomDatabaseOption(options, "LogParameters", e => e.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase), () => false);
        if (logParameters)
        {
            connectionBuilder.LogParameters = logParameters;
        }
    }

    /// <summary>
    /// Gets the connection builder resolved during <see cref="Initialise"/>, falling back to
    /// environment variables when it has not been initialised yet (e.g. design time usage).
    /// </summary>
    private NpgsqlConnectionStringBuilder GetRuntimeConnectionBuilder()
        => _connectionBuilder ?? GetConnectionBuilder();

    /// <summary>
    /// Passes the connection's SSL settings to libpq through its environment variables.
    /// pg_dump and psql do not read the Npgsql connection string, so without this they
    /// would connect with libpq's defaults even when the runtime connection verifies the
    /// server certificate.
    /// </summary>
    /// <param name="startInfo">The process to configure.</param>
    /// <param name="connectionBuilder">The connection the tool must use.</param>
    private static void ApplyPgEnvironment(ProcessStartInfo startInfo, NpgsqlConnectionStringBuilder connectionBuilder)
    {
        startInfo.Environment["PGPASSWORD"] = connectionBuilder.Password;
        startInfo.Environment["PGSSLMODE"] = ToLibpqSslMode(connectionBuilder.SslMode);

        if (!string.IsNullOrWhiteSpace(connectionBuilder.RootCertificate))
        {
            startInfo.Environment["PGSSLROOTCERT"] = connectionBuilder.RootCertificate;
        }

        if (!string.IsNullOrWhiteSpace(connectionBuilder.SslCertificate))
        {
            startInfo.Environment["PGSSLCERT"] = connectionBuilder.SslCertificate;
        }

        if (!string.IsNullOrWhiteSpace(connectionBuilder.SslKey))
        {
            startInfo.Environment["PGSSLKEY"] = connectionBuilder.SslKey;
        }

        if (!string.IsNullOrWhiteSpace(connectionBuilder.SslPassword))
        {
            startInfo.Environment["PGSSLPASSWORD"] = connectionBuilder.SslPassword;
        }
    }

    /// <summary>
    /// Converts Npgsql's SSL mode to the spelling libpq expects.
    /// </summary>
    /// <param name="sslMode">The connection's SSL mode.</param>
    /// <returns>The matching libpq sslmode value.</returns>
    private static string ToLibpqSslMode(SslMode sslMode) => sslMode switch
    {
        SslMode.Disable => "disable",
        SslMode.Allow => "allow",
        SslMode.Prefer => "prefer",
        SslMode.Require => "require",
        SslMode.VerifyCA => "verify-ca",
        SslMode.VerifyFull => "verify-full",
        _ => "prefer"
    };

    private NpgsqlConnectionStringBuilder GetConnectionBuilder()
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("PostgreSQL password must be provided via POSTGRES_PASSWORD environment variable");
        }

        var connectionBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = GetEnvironmentValue("POSTGRES_HOST", "jellyfin"),
            Port = int.Parse(GetEnvironmentValue("POSTGRES_PORT", "5432"), CultureInfo.InvariantCulture),
            Database = GetEnvironmentValue("POSTGRES_DB", "jellyfin"),
            Username = GetEnvironmentValue("POSTGRES_USER", "jellyfin"),
            Password = password,

            // Command timeout in seconds (0 = no limit). Defaults to Npgsql's 30s.
            // Raise it via POSTGRES_COMMAND_TIMEOUT for slow queries on large libraries.
            CommandTimeout = int.Parse(GetEnvironmentValue("POSTGRES_COMMAND_TIMEOUT", "30"), CultureInfo.InvariantCulture)
        };

        return connectionBuilder;
    }
}
