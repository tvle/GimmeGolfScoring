using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using iDoublePress.Models;

namespace iDoublePress.Data;

/// <summary>
/// Base class for repositories providing thread-safe initialization and common functionality.
/// This ensures consistent initialization patterns across all repositories in the golf scoring app.
/// </summary>
public abstract class RepositoryBase
{
    private static readonly SemaphoreSlim MigrationSemaphore = new(1, 1);
    private static volatile bool _databaseMigrated;

    protected readonly SemaphoreSlim _initSemaphore = new(1, 1);
    protected volatile bool _hasBeenInitialized = false;
    protected readonly ILogger _logger;
    
    protected RepositoryBase(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Ensures the repository is initialized in a thread-safe manner.
    /// Uses double-checked locking pattern to prevent race conditions during initialization.
    /// </summary>
    protected async Task EnsureInitializedAsync()
    {
        if (_hasBeenInitialized)
            return;
            
        await _initSemaphore.WaitAsync();
        try
        {
            if (_hasBeenInitialized)
                return;

            await EnsureDatabaseMigratedAsync();
            await InitializeInternalAsync();
            _hasBeenInitialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Internal initialization method to be implemented by derived repositories.
    /// This method is called only once per repository instance.
    /// </summary>
    protected abstract Task InitializeInternalAsync();
    
    /// <summary>
    /// Creates a new database connection with proper error handling.
    /// </summary>
    /// <returns>A task that resolves to an opened database connection</returns>
    protected async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await DatabaseMigrator.ConfigureConnectionAsync(connection);
        return connection;
    }

    private static async Task EnsureDatabaseMigratedAsync()
    {
        if (_databaseMigrated)
            return;

        await MigrationSemaphore.WaitAsync();
        try
        {
            if (_databaseMigrated)
                return;

            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();
            await DatabaseMigrator.MigrateAsync(connection);
            _databaseMigrated = true;
        }
        finally
        {
            MigrationSemaphore.Release();
        }
    }

    protected static void MarkEntityForUpsert(ISyncEntity entity)
    {
        entity.PublicId = string.IsNullOrWhiteSpace(entity.PublicId)
            ? Guid.NewGuid().ToString("D")
            : entity.PublicId;
        entity.SyncUpdatedAtUtc = DateTime.UtcNow;
        entity.IsDeleted = false;
        entity.DeletedAtUtc = null;
        entity.PendingSync = true;
    }

    protected static void MarkEntityForDelete(ISyncEntity entity)
    {
        entity.PublicId = string.IsNullOrWhiteSpace(entity.PublicId)
            ? Guid.NewGuid().ToString("D")
            : entity.PublicId;
        entity.SyncUpdatedAtUtc = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.PendingSync = true;
    }

    protected async Task QueueOutboxAsync(SqliteConnection connection, SqliteTransaction? transaction, string entityType, ISyncEntity entity, string operation)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO SyncOutbox (EntityType, EntityPublicId, Operation, QueuedAtUtc, AttemptCount, LastError)
            VALUES (@EntityType, @EntityPublicId, @Operation, @QueuedAtUtc, 0, NULL)
            ON CONFLICT(EntityType, EntityPublicId) DO UPDATE SET
                Operation = excluded.Operation,
                QueuedAtUtc = excluded.QueuedAtUtc,
                AttemptCount = 0,
                LastError = NULL;";
        cmd.Parameters.AddWithValue("@EntityType", entityType);
        cmd.Parameters.AddWithValue("@EntityPublicId", entity.PublicId);
        cmd.Parameters.AddWithValue("@Operation", operation);
        cmd.Parameters.AddWithValue("@QueuedAtUtc", DatabaseDateTime.ToUtcString(DateTime.UtcNow));
        await cmd.ExecuteNonQueryAsync();
    }
}