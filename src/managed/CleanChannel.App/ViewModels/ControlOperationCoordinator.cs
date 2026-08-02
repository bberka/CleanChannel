namespace CleanChannel.App.ViewModels;

internal sealed class ControlOperationCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _lifecycleGate = new();
    private readonly HashSet<Task> _activeOperations = new();
    private bool _acceptingOperations = true;
    private bool _isDisposed;
    private Task? _drainTask;

    public void StopAccepting()
    {
        lock (_lifecycleGate)
        {
            _acceptingOperations = false;
        }
    }

    public Task<T?> RunAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_lifecycleGate)
        {
            if (!_acceptingOperations || _isDisposed)
            {
                return Task.FromResult<T?>(default);
            }

            var task = RunCoreAsync(operation);
            _activeOperations.Add(task);
            _ = task.ContinueWith(
                completedTask => OperationCompleted(completedTask),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }
    }

    public Task DrainAsync()
    {
        lock (_lifecycleGate)
        {
            _acceptingOperations = false;
            _drainTask ??= DrainCoreAsync(_activeOperations.ToArray());
            return _drainTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DrainAsync().ConfigureAwait(true);
        DisposeGate();
    }

    private async Task<T?> RunCoreAsync<T>(Func<Task<T>> operation)
    {
        await _operationGate.WaitAsync().ConfigureAwait(true);

        try
        {
            return await operation().ConfigureAwait(true);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task DrainCoreAsync(IReadOnlyCollection<Task> operations)
    {
        await Task.WhenAll(operations).ConfigureAwait(true);

        lock (_lifecycleGate)
        {
            _activeOperations.Clear();
        }
    }

    private void OperationCompleted(Task completedTask)
    {
        lock (_lifecycleGate)
        {
            _activeOperations.Remove(completedTask);
        }
    }

    private void DisposeGate()
    {
        lock (_lifecycleGate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _operationGate.Dispose();
        }
    }
}
