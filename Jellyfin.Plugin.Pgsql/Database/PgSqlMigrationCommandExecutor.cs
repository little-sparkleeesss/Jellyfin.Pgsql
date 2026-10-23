#pragma warning disable EF1001 // The migration executor is an internal EF Core API, pinned to the package version.
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Jellyfin.Plugin.Pgsql.Database;

/// <summary>
/// Executes EF migration commands without the runtime command timeout.
/// </summary>
/// <remarks>
/// Jellyfin runs the plugin's migrations through the same DbContext, and therefore the
/// same connection string, as regular queries. POSTGRES_COMMAND_TIMEOUT (default 30s) is
/// meant for queries, but migrations routinely exceed it: the 12.1 migration normalises
/// every BaseItems row and builds indexes on large libraries. A timeout aborts the
/// migration transaction and Jellyfin then attempts a rollback, so the timeout has to be
/// lifted for migrations while staying in force for normal queries. EF applies
/// <see cref="IRelationalConnection.CommandTimeout"/> to every command it executes, so
/// this executor clears it only for the duration of the migration commands.
/// </remarks>
public sealed class PgSqlMigrationCommandExecutor : MigrationCommandExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlMigrationCommandExecutor"/> class.
    /// </summary>
    /// <param name="executionStrategy">The execution strategy EF uses for migration commands.</param>
    public PgSqlMigrationCommandExecutor(IExecutionStrategy executionStrategy)
        : base(executionStrategy)
    {
    }

    /// <inheritdoc/>
    public override int ExecuteNonQuery(
        IReadOnlyList<MigrationCommand> migrationCommands,
        IRelationalConnection connection,
        MigrationExecutionState executionState,
        bool commitTransaction,
        IsolationLevel? isolationLevel = null)
    {
        var previousTimeout = connection.CommandTimeout;
        connection.CommandTimeout = 0;
        try
        {
            return base.ExecuteNonQuery(migrationCommands, connection, executionState, commitTransaction, isolationLevel);
        }
        finally
        {
            connection.CommandTimeout = previousTimeout;
        }
    }

    /// <inheritdoc/>
    public override async Task<int> ExecuteNonQueryAsync(
        IReadOnlyList<MigrationCommand> migrationCommands,
        IRelationalConnection connection,
        MigrationExecutionState executionState,
        bool commitTransaction,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        var previousTimeout = connection.CommandTimeout;
        connection.CommandTimeout = 0;
        try
        {
            return await base.ExecuteNonQueryAsync(migrationCommands, connection, executionState, commitTransaction, isolationLevel, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            connection.CommandTimeout = previousTimeout;
        }
    }
}
#pragma warning restore EF1001
