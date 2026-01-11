namespace iDoublePress.Utilities;

/// <summary>
/// Utility class for debouncing asynchronous operations to prevent race conditions
/// during rapid user input, such as hole scoring in golf rounds.
/// </summary>
public class Debouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _lock = new();
    
    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
    }
    
    /// <summary>
    /// Debounces the provided action, ensuring it's only executed once after
    /// a period of no new calls. This prevents race conditions during rapid
    /// property changes in hole scoring.
    /// </summary>
    /// <param name="action">The async action to debounce</param>
    public void Debounce(Func<Task> action)
    {
        lock (_lock)
        {
            // Cancel any pending operation
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Start new delayed operation
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
                            // Log error but don't crash the app
                            System.Diagnostics.Debug.WriteLine($"Debounced action failed: {ex}");
                            // In a real app, you might want to use proper logging here
                        }
                    }
                }, TaskScheduler.Default);
        }
    }
    
    /// <summary>
    /// Debounces the provided action with a result, ensuring it's only executed once
    /// after a period of no new calls.
    /// </summary>
    /// <typeparam name="T">The return type of the action</typeparam>
    /// <param name="action">The async action to debounce</param>
    /// <returns>A task that will eventually contain the result</returns>
    public Task<T> Debounce<T>(Func<Task<T>> action)
    {
        var taskCompletionSource = new TaskCompletionSource<T>();
        
        lock (_lock)
        {
            // Cancel any pending operation
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Start new delayed operation
            Task.Delay(_delay, _cancellationTokenSource.Token)
                .ContinueWith(async _ =>
                {
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var result = await action();
                            taskCompletionSource.TrySetResult(result);
                        }
                        catch (Exception ex)
                        {
                            taskCompletionSource.TrySetException(ex);
                            System.Diagnostics.Debug.WriteLine($"Debounced action failed: {ex}");
                        }
                    }
                    else
                    {
                        taskCompletionSource.TrySetCanceled();
                    }
                }, TaskScheduler.Default);
        }
        
        return taskCompletionSource.Task;
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