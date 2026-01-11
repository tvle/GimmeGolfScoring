using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace iDoublePress.Data;

/// <summary>
/// Base class for repositories providing thread-safe initialization and common functionality.
/// This ensures consistent initialization patterns across all repositories in the golf scoring app.
/// </summary>
public abstract class RepositoryBase
{
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
        return connection;
    }
}