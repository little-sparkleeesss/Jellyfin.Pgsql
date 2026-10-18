using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Pgsql.Database;

/// <summary>
/// Buffers the one query Jellyfin's <c>MigrateRatingLevels</c> routine enumerates while it writes.
/// </summary>
/// <remarks>
/// <para>
/// The routine (added in Jellyfin 12.1, unchanged in 12.2) enumerates
/// <c>BaseItems.AsNoTracking().Select(e =&gt; e.OfficialRating).Distinct()</c> without materialising
/// it, and calls <c>ExecuteUpdate</c> on the same <c>DbContext</c> from inside that loop. SQLite
/// tolerates a second command while a reader is open; PostgreSQL does not, so Npgsql throws
/// <c>NpgsqlOperationInProgressException</c> and startup aborts. The routine is an
/// AppInitialisation stage migration, so it runs on any existing database that has at least one
/// <c>BaseItem</c> - see the Known limitations section of the README.
/// </para>
/// <para>
/// Draining the reader before handing it back leaves the connection idle, so the routine's
/// <c>ExecuteUpdate</c> runs normally in the same transaction (EF sets <c>DbCommand.Transaction</c>
/// before this interceptor is invoked) and the routine records itself as applied. This is the
/// provider-side equivalent of upstream materialising the query: the routine still recomputes the
/// inherited parental rating of every item, which is what 12.2 re-runs it for.
/// </para>
/// <para>
/// Re-check this on every Jellyfin upgrade. If upstream materialises the query (adding
/// <c>.ToList()</c> is the one-line fix), delete this file and its registration in
/// <c>PgSqlDatabaseProvider.Initialise</c>: the interception keeps matching harmlessly until then,
/// because the generated SQL does not change. If upstream rewrites the query without materialising
/// it, the interception stops matching and startup aborts as described above, without the
/// "Buffered" log line; widen <see cref="IsRatingLevelsQuery"/> in that case.
/// </para>
/// </remarks>
internal sealed class MigrateRatingLevelsInterceptor : DbCommandInterceptor
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrateRatingLevelsInterceptor"/> class.
    /// </summary>
    /// <param name="logger">A logger.</param>
    public MigrateRatingLevelsInterceptor(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        // Another interceptor may already have produced a reader; leave it alone.
        if (result.HasResult || !IsRatingLevelsQuery(command.CommandText))
        {
            return result;
        }

        using var reader = command.ExecuteReader();
        return InterceptionResult<DbDataReader>.SuppressWithResult(Buffer(reader, command.CommandText));
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (result.HasResult || !IsRatingLevelsQuery(command.CommandText))
        {
            return result;
        }

        // The routine enumerates synchronously, so this only matters if upstream moves it to
        // await foreach - but then it would need the same treatment.
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            return InterceptionResult<DbDataReader>.SuppressWithResult(Buffer(reader, command.CommandText));
        }
    }

    /// <summary>
    /// Drains a reader into memory and returns a reader over the copy.
    /// </summary>
    /// <param name="reader">The reader to drain. Disposed by the caller.</param>
    /// <param name="commandText">The SQL that produced the reader.</param>
    /// <returns>A reader over the drained rows.</returns>
    private DataTableReader Buffer(DbDataReader reader, string commandText)
    {
        var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        table.Load(reader);

        // The Information line is the first thing to look for when diagnosing a startup abort in
        // this routine; the Debug line shows the matched SQL when more detail is needed.
        _logger.LogDebug("Buffered query: {CommandText}", commandText);

        _logger.LogInformation(
            "Buffered {RowCount} rows of the MigrateRatingLevels rating query so the routine can update the items it enumerates",
            table.Rows.Count);

        return table.CreateDataReader();
    }

    /// <summary>
    /// Recognises the query the routine enumerates: a single column projection of
    /// <c>OfficialRating</c> over <c>BaseItems</c>.
    /// </summary>
    /// <param name="commandText">The SQL about to be executed.</param>
    /// <returns>Whether the query is the one to buffer.</returns>
    /// <remarks>
    /// Matched on keywords rather than on the exact text, so that upstream adding a WHERE or
    /// ORDER BY clause does not silently stop the interception. The single column requirement keeps
    /// the match narrow: the only other query in Jellyfin with this shape is the list of known
    /// official ratings behind /Items/Filters, which is fully materialised by its caller anyway.
    /// </remarks>
    private static bool IsRatingLevelsQuery(string commandText)
    {
        var sql = commandText.AsSpan().TrimStart();
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fromIndex = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        if (fromIndex < 6)
        {
            return false;
        }

        var selectList = sql[6..fromIndex];
        return !selectList.Contains(',')
            && selectList.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase)
            && selectList.Contains("OfficialRating", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("BaseItems", StringComparison.OrdinalIgnoreCase);
    }
}
