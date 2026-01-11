# Implementation Plan for Database Stability Issues

## Overview

This document provides a detailed implementation plan for addressing the timing, race condition, and stability issues identified in the data access layer. The plan is organized by priority and includes specific code changes, testing requirements, and migration considerations.

## Priority 1: Critical Stability Issues

### 1.1 Fix Race Condition in Repository Initialization

**Files to modify:**
- `iDoublePress/Data/RoundRepository.cs`
- `iDoublePress/Data/PlayerRepository.cs`
- `iDoublePress/Data/CourseRepository.cs`

**Changes required:**

1. **Create a RepositoryBase class:**
```csharp
// iDoublePress/Data/RepositoryBase.cs
public abstract class RepositoryBase
{
    protected readonly SemaphoreSlim _initSemaphore = new(1, 1);
    protected volatile bool _hasBeenInitialized = false;
    protected readonly ILogger _logger;
    
    protected RepositoryBase(ILogger logger)
    {
        _logger = logger;
    }
    
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
    
    protected abstract Task InitializeInternalAsync();
}
```

2. **Update RoundRepository to inherit from RepositoryBase:**
```csharp
// iDoublePress/Data/RoundRepository.cs
public class RoundRepository : RepositoryBase
{
    // Remove: private volatile bool _hasBeenInitialized = false;
    // Remove: private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    
    public RoundRepository(CourseRepository courseRepository, PlayerRepository playerRepository, ILogger<RoundRepository> logger) 
        : base(logger)
    {
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
    }
    
    private async Task Init()
    {
        await EnsureInitializedAsync();
    }
    
    protected override async Task InitializeInternalAsync()
    {
        // Move existing initialization code here
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        
        // ... existing initialization logic
    }
}
```

3. **Update PlayerRepository and CourseRepository:**
```csharp
// iDoublePress/Data/PlayerRepository.cs
public class PlayerRepository : RepositoryBase
{
    // Remove: private bool _hasBeenInitialized = false;
    
    public PlayerRepository(ILogger<PlayerRepository> logger) 
        : base(logger)
    {
    }
    
    private async Task Init()
    {
        await EnsureInitializedAsync();
    }
    
    protected override async Task InitializeInternalAsync()
    {
        // Move existing initialization code here
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        
        // ... existing initialization logic
    }
}
```

### 1.2 Fix Auto-save Race Condition

**Files to modify:**
- `iDoublePress/PageModels/ActiveRoundPageModel.cs`

**Changes required:**

1. **Add Debouncer utility class:**
```csharp
// iDoublePress/Utilities/Debouncer.cs
public class Debouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _lock = new();
    
    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
    }
    
    public void Debounce(Func<Task> action)
    {
        lock (_lock)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            
            Task.Delay(_delay, _cancellationTokenSource.Token)
                .ContinueWith(async _ =>
                {
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await action();
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't crash
                            System.Diagnostics.Debug.WriteLine($"Debounced action failed: {ex}");
                        }
                    }
                }, TaskScheduler.Default);
        }
    }
    
    public void Dispose()
    {
        lock (_lock)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
```

2. **Update ActiveRoundPageModel:**
```csharp
// iDoublePress/PageModels/ActiveRoundPageModel.cs
public partial class ActiveRoundPageModel : ObservableObject
{
    private readonly Debouncer _holeSaveDebouncer;
    
    public ActiveRoundPageModel(RoundRepository roundRepository, ModalErrorHandler errorHandler)
    {
        _roundRepository = roundRepository;
        _errorHandler = errorHandler;
        _holeSaveDebouncer = new Debouncer(TimeSpan.FromMilliseconds(150));
        
        // Add cleanup in destructor or OnDisappearing
    }
    
    private async void CurrentHole_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Hole hole) return;

        if (e.PropertyName is nameof(Hole.Score))
        {
            hole.IsScored = true;
            OnPropertyChanged(nameof(CanDecreasePutts));
            OnPropertyChanged(nameof(CanIncreasePutts));
        }

        if (e.PropertyName is nameof(Hole.FairwayResult) or nameof(Hole.Penalties) or nameof(Hole.GreenInRegulation) or nameof(Hole.Putts) or nameof(Hole.Proximity))
        {
            EnsureStatsDefaults(hole);

            // Putts constraint: 0..Score; null allowed.
            if (hole.Putts.HasValue)
            {
                var clamped = Math.Clamp(hole.Putts.Value, 0, hole.Score);
                if (clamped != hole.Putts.Value)
                {
                    hole.Putts = clamped;
                }
            }

            _holeSaveDebouncer.Debounce(async () => 
            {
                await SaveHoleOnlyAsync(hole);
            });
        }
    }
    
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _holeSaveDebouncer.Dispose();
    }
}
```

### 1.3 Standardize Repository Initialization

**Files to modify:**
- `iDoublePress/Data/PlayerRepository.cs`
- `iDoublePress/Data/CourseRepository.cs`

**Changes required:**
1. **Update both repositories to use the RepositoryBase pattern** (as shown in section 1.1)

## Priority 2: Performance Improvements

### 2.1 Implement Scoped Write Semaphores

**Files to modify:**
- `iDoublePress/Data/RoundRepository.cs`

**Changes required:**

```csharp
public class RoundRepository : RepositoryBase
{
    private readonly SemaphoreSlim _roundWriteSemaphore = new(1, 1);
    private readonly SemaphoreSlim _holeWriteSemaphore = new(3, 3); // Allow concurrent hole writes
    
    public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            
            // ... existing create round logic
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }
    
    public async Task<int> SaveItemAsync(Round item)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            
            // ... existing save logic
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }
    
    public async Task SaveHoleAsync(Hole hole)
    {
        await _holeWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            
            // ... existing save hole logic
        }
        finally
        {
            _holeWriteSemaphore.Release();
        }
    }
    
    public async Task<int> DeleteItemAsync(Round item)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            
            // ... existing delete logic
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }
}
```

### 2.2 Add Connection Pooling

**Files to modify:**
- `iDoublePress/Data/Constants.cs`
- `iDoublePress/Data/RepositoryBase.cs`

**Changes required:**

1. **Update Constants.cs:**
```csharp
public static class Constants
{
    public const string DatabaseFilename = "AppSQLite.db3";
    
    // Add connection pooling settings
    public const int ConnectionPoolSize = 10;
    public const int ConnectionPoolTimeout = 30; // seconds
    
    public static string DatabasePath =>
        $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename)};Pooling=True;Max Pool Size={ConnectionPoolSize};Pool Timeout={ConnectionPoolTimeout}";
}
```

2. **Update RepositoryBase:**
```csharp
public abstract class RepositoryBase
{
    // ... existing code
    
    protected async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        return connection;
    }
}
```

### 2.3 Improve Database Integrity Checks

**Files to modify:**
- `iDoublePress/Data/RoundRepository.cs`

**Changes required:**

```csharp
private async Task<bool> CheckDatabaseIntegrityAsync(SqliteConnection connection)
{
    try
    {
        // Use PRAGMA quick_check for basic integrity verification
        var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA quick_check;";
        LogSql(cmd, "PRAGMA quick_check");
        
        var result = await cmd.ExecuteScalarAsync();
        var resultString = result?.ToString();
        
        if (string.Equals(resultString, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        else
        {
            _logger.LogWarning("Database integrity check failed: {Result}", resultString);
            return false;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Database integrity check failed");
        return false;
    }
}
```

## Priority 3: Reliability Enhancements

### 3.1 Add Retry Logic

**Files to create:**
- `iDoublePress/Utilities/RetryHelper.cs`

**Code:**
```csharp
public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        int initialDelayMs = 100,
        ILogger? logger = null)
    {
        int retryCount = 0;
        int delayMs = initialDelayMs;
        
        while (true)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                retryCount++;
                logger?.LogWarning(ex, "Operation failed, retrying {RetryCount}/{MaxRetries}", retryCount, maxRetries);
                
                await Task.Delay(delayMs);
                delayMs = (int)(delayMs * 1.5); // Exponential backoff
            }
        }
    }
    
    public static async Task ExecuteWithRetryAsync(
        Func<Task> action,
        int maxRetries = 3,
        int initialDelayMs = 100,
        ILogger? logger = null)
    {
        await ExecuteWithRetryAsync(async () => 
        {
            await action();
            return true;
        }, maxRetries, initialDelayMs, logger);
    }
}
```

### 3.2 Update Repository Methods to Use Retry Logic

**Files to modify:**
- `iDoublePress/Data/RoundRepository.cs`

**Changes required:**
```csharp
public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
{
    return await RetryHelper.ExecuteWithRetryAsync(async () =>
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            
            // ... existing create round logic
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }, logger: _logger);
}
```

## Testing Requirements

### Unit Tests

1. **Repository Initialization Tests:**
```csharp
[TestClass]
public class RepositoryInitializationTests
{
    [TestMethod]
    public async Task Init_ShouldBeThreadSafe()
    {
        var logger = new Mock<ILogger<RoundRepository>>();
        var courseRepo = new Mock<CourseRepository>();
        var playerRepo = new Mock<PlayerRepository>();
        
        var repository = new RoundRepository(courseRepo.Object, playerRepo.Object, logger.Object);
        
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () => await repository.Init()));
        }
        
        await Task.WhenAll(tasks);
        
        // Verify initialization completed without exceptions
    }
}
```

2. **Auto-save Debouncing Tests:**
```csharp
[TestClass]
public class ActiveRoundPageModelTests
{
    [TestMethod]
    public async Task CurrentHole_PropertyChanged_ShouldDebounceSaves()
    {
        var mockRepo = new Mock<RoundRepository>();
        var model = new ActiveRoundPageModel(mockRepo.Object, new ModalErrorHandler());
        
        var hole = new Hole();
        
        // Simulate rapid property changes
        for (int i = 0; i < 5; i++)
        {
            hole.Score++;
            model.OnPropertyChanged(nameof(Hole.Score));
            await Task.Delay(50); // Faster than debounce delay
        }
        
        // Wait for debounce to complete
        await Task.Delay(200);
        
        // Verify save was called only once
        mockRepo.Verify(x => x.SaveHoleAsync(It.IsAny<Hole>()), Times.Once);
    }
}
```

### Integration Tests

1. **Concurrent Write Tests:**
```csharp
[TestClass]
public class ConcurrentWriteTests
{
    [TestMethod]
    public async Task MultipleRounds_ShouldNotBlockEachOther()
    {
        var logger = new Mock<ILogger<RoundRepository>>();
        var courseRepo = new Mock<CourseRepository>();
        var playerRepo = new Mock<PlayerRepository>();
        
        var repository = new RoundRepository(courseRepo.Object, playerRepo.Object, logger.Object);
        
        var tasks = new List<Task<Round>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(repository.CreateNewRoundAsync(1, 1));
        }
        
        var rounds = await Task.WhenAll(tasks);
        
        // Verify all rounds were created successfully
        Assert.AreEqual(5, rounds.Length);
    }
}
```

## Migration Plan

### Phase 1: Critical Fixes (Week 1-2)

1. **Create RepositoryBase class**
2. **Update RoundRepository to inherit from RepositoryBase**
3. **Fix auto-save race condition in ActiveRoundPageModel**
4. **Update PlayerRepository and CourseRepository to use RepositoryBase**

### Phase 2: Performance Improvements (Week 3-4)

1. **Implement scoped write semaphores**
2. **Add connection pooling**
3. **Improve database integrity checks**

### Phase 3: Reliability Enhancements (Week 5-6)

1. **Add retry logic**
2. **Update all repository methods to use retry logic**
3. **Add comprehensive logging**

### Phase 4: Testing and Validation (Week 7-8)

1. **Write unit tests for all changes**
2. **Run integration tests**
3. **Performance testing and optimization**

## Rollout Strategy

1. **Staged Rollout:**
   - Deploy to a small percentage of users first
   - Monitor for errors and performance issues
   - Gradually increase rollout percentage

2. **Monitoring:**
   - Track database operation performance
   - Monitor for exceptions and errors
   - Watch for memory leaks and resource usage

3. **Rollback Plan:**
   - Keep backup of original files
   - Monitor key metrics for 24 hours after deployment
   - Be prepared to rollback if critical issues are found

## Success Criteria

1. **No race conditions** in repository initialization
2. **Improved performance** under concurrent load
3. **No data corruption** from concurrent operations
4. **Reduced crashes** from database errors
5. **Better user experience** with responsive UI

This implementation plan addresses all the identified timing, race condition, and stability issues while maintaining backward compatibility and following best practices for database access in .NET MAUI applications.