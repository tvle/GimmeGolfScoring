# Database Stability Analysis: Timing, Race Conditions, and Instability Issues

## Executive Summary

After analyzing the data folder and RoundRepository, I've identified several critical timing, race conditions, and stability issues that could lead to data corruption, performance problems, and application crashes. This document provides a comprehensive analysis of the issues and a mitigation plan.

## Critical Issues Identified

### 1. **Race Condition in RoundRepository Initialization**

**Location**: [`RoundRepository.cs:16-67`](iDoublePress/Data/RoundRepository.cs:16-67)

**Issue**: The initialization pattern has a race condition between the `volatile bool _hasBeenInitialized` check and the semaphore wait.

```csharp
private async Task Init()
{
    if (_hasBeenInitialized)  // Race condition here
        return;

    await _initSemaphore.WaitAsync();
    try
    {
        if (_hasBeenInitialized)  // Double-check, but race window exists
            return;
        // ... initialization code
    }
    finally
    {
        _initSemaphore.Release();
    }
}
```

**Impact**: Multiple threads could potentially initialize the database simultaneously, leading to:
- Duplicate table creation attempts
- Foreign key constraint violations
- Performance degradation from concurrent DDL operations

### 2. **Write Semaphore Contention**

**Location**: [`RoundRepository.cs:408-481`](iDoublePress/Data/RoundRepository.cs:408-481), [`RoundRepository.cs:504-572`](iDoublePress/Data/RoundRepository.cs:504-572)

**Issue**: A single global write semaphore (`_writeSemaphore`) blocks all write operations, including unrelated ones.

```csharp
public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
{
    await _writeSemaphore.WaitAsync();  // Blocks all writes
    try
    {
        // ... create round logic
    }
    finally
    {
        _writeSemaphore.Release();
    }
}
```

**Impact**:
- Poor performance when multiple operations need to write
- Deadlock potential if operations wait for each other
- User experience degradation during concurrent operations

### 3. **Database Integrity Check Performance Issues**

**Location**: [`RoundRepository.cs:483-500`](iDoublePress/Data/RoundRepository.cs:483-500)

**Issue**: The current "lightweight" integrity check is insufficient and may miss corruption.

```csharp
private async Task<bool> CheckDatabaseIntegrityAsync(SqliteConnection connection)
{
    // Use a very small, safe query against sqlite_master instead of `PRAGMA integrity_check`
    // `integrity_check` can trigger heavy internal parsing and has previously crashed on corrupted DB files.
    try
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' LIMIT 1;";
        LogSql(cmd, "CheckDatabaseIntegrity");
        var result = await cmd.ExecuteScalarAsync();
        return true;  // This check is meaningless for integrity
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Lightweight DB read failed � treating database as possibly corrupt");
        return false;
    }
}
```

**Impact**:
- False positives/negatives in corruption detection
- Risk of data corruption going undetected
- Application crashes from corrupted databases

### 4. **Auto-save Race Condition in ActiveRoundPageModel**

**Location**: [`ActiveRoundPageModel.cs:461-511`](iDoublePress/PageModels/ActiveRoundPageModel.cs:461-511)

**Issue**: Debounced save mechanism has race conditions and potential memory leaks.

```csharp
private CancellationTokenSource? _holeSaveCts;

private async void CurrentHole_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    // ... property change handling
    
    _holeSaveCts?.Cancel();  // Race condition: cancellation may not complete
    _holeSaveCts = new CancellationTokenSource();
    var token = _holeSaveCts.Token;

    try
    {
        await Task.Delay(150, token);
        await SaveHoleOnlyAsync(hole);
    }
    catch (OperationCanceledException)
    {
        return;
    }
}
```

**Impact**:
- Multiple concurrent save operations
- Potential memory leaks from orphaned CancellationTokenSource objects
- Data loss from cancelled saves

### 5. **Inconsistent Repository Initialization Patterns**

**Location**: [`PlayerRepository.cs:20-49`](iDoublePress/Data/PlayerRepository.cs:20-49), [`CourseRepository.cs:20-70`](iDoublePress/Data/CourseRepository.cs:20-70)

**Issue**: Different repositories use different initialization patterns, leading to inconsistent behavior.

```csharp
// PlayerRepository - no semaphore
private async Task Init()
{
    if (_hasBeenInitialized)
        return;

    await using var connection = new SqliteConnection(Constants.DatabasePath);
    // ... no synchronization
}

// RoundRepository - has semaphore
private async Task Init()
{
    if (_hasBeenInitialized)
        return;

    await _initSemaphore.WaitAsync();
    // ... with synchronization
}
```

**Impact**:
- Inconsistent thread safety across repositories
- Potential race conditions when repositories interact
- Difficult to maintain and debug

### 6. **Database Connection Management Issues**

**Location**: Throughout all repository classes

**Issue**: No connection pooling, each operation creates new connections.

```csharp
await using var connection = new SqliteConnection(Constants.DatabasePath);
await connection.OpenAsync();
```

**Impact**:
- Performance overhead from connection creation
- Resource exhaustion under high load
- Potential connection leaks

### 7. **Transaction Management Issues**

**Location**: [`RoundRepository.cs:431-475`](iDoublePress/Data/RoundRepository.cs:431-475)

**Issue**: Transactions are not properly scoped and may not handle all error conditions.

```csharp
using var transaction = connection.BeginTransaction();
try
{
    // ... multiple operations
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

**Impact**:
- Data inconsistency from partial operations
- Deadlock potential
- Resource leaks from uncommitted transactions

## Mitigation Strategy

### Priority 1: Critical Stability Issues

1. **Fix Race Condition in Init()**
   - Implement proper double-checked locking with volatile field
   - Add exception handling to prevent incomplete initialization

2. **Fix Auto-save Race Condition**
   - Implement proper debouncing with cancellation
   - Add memory leak prevention
   - Ensure save operations are atomic

3. **Standardize Repository Initialization**
   - Use consistent initialization pattern across all repositories
   - Implement proper thread safety

### Priority 2: Performance and Reliability

4. **Optimize Write Semaphore Usage**
   - Implement scoped semaphores for different operation types
   - Add timeout handling to prevent deadlocks

5. **Improve Database Integrity Checks**
   - Implement proper integrity checking strategy
   - Add corruption recovery mechanisms

6. **Add Connection Pooling**
   - Implement connection reuse
   - Add connection timeout handling

### Priority 3: Long-term Stability

7. **Add Retry Logic**
   - Implement retry mechanism for transient failures
   - Add exponential backoff

8. **Add Comprehensive Logging**
   - Add detailed logging for debugging
   - Monitor performance metrics

## Implementation Plan

### Phase 1: Critical Fixes (Week 1-2)
- Fix Init() race condition in RoundRepository
- Fix auto-save race condition in ActiveRoundPageModel
- Standardize repository initialization patterns

### Phase 2: Performance Improvements (Week 3-4)
- Implement scoped write semaphores
- Add connection pooling
- Improve database integrity checks

### Phase 3: Reliability Enhancements (Week 5-6)
- Add retry logic for transient failures
- Implement comprehensive logging
- Add performance monitoring

### Phase 4: Testing and Validation (Week 7-8)
- Unit tests for concurrency scenarios
- Integration tests for repository interactions
- Performance testing and optimization

## Recommended Architecture Changes

### 1. Repository Base Class
```csharp
public abstract class RepositoryBase
{
    protected readonly SemaphoreSlim _initSemaphore = new(1, 1);
    protected volatile bool _hasBeenInitialized = false;
    
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

### 2. Scoped Write Semaphores
```csharp
public class RoundRepository
{
    private readonly SemaphoreSlim _roundWriteSemaphore = new(1, 1);
    private readonly SemaphoreSlim _holeWriteSemaphore = new(3, 3); // Allow concurrent hole writes
    
    public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            // ... create round logic
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }
}
```

### 3. Improved Auto-save Mechanism
```csharp
public class ActiveRoundPageModel
{
    private readonly Debouncer _holeSaveDebouncer = new Debouncer(150);
    
    private async void CurrentHole_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Hole.Score) or nameof(Hole.Putts))
        {
            _holeSaveDebouncer.Debounce(async () => 
            {
                await SaveHoleOnlyAsync(hole);
            });
        }
    }
}
```

## Testing Strategy

### Unit Tests
- Test race conditions in repository initialization
- Test concurrent write operations
- Test auto-save debouncing and cancellation

### Integration Tests
- Test repository interactions under load
- Test transaction integrity
- Test database corruption recovery

### Performance Tests
- Measure impact of optimizations
- Test under high concurrency
- Monitor memory usage and leaks

## Conclusion

The current implementation has several critical stability and performance issues that need immediate attention. The proposed fixes will significantly improve the reliability and performance of the data access layer while maintaining backward compatibility.

The recommended approach is to implement the critical fixes first (Phase 1) to address the most urgent stability issues, followed by performance improvements and reliability enhancements in subsequent phases.