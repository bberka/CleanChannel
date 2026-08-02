using CommunityToolkit.Mvvm.Input;
using CleanChannel.App.ViewModels;
using Microsoft.Extensions.Logging;

namespace CleanChannel.App.Shortcuts;

public sealed class GlobalShortcutCoordinator : IAsyncDisposable
{
    private readonly IGlobalShortcutService _shortcutService;
    private readonly MainWindowViewModel _viewModel;
    private readonly ILogger? _logger;
    private readonly object _callbackGate = new();
    private readonly HashSet<Task> _activeCallbackTasks = new();
    private bool _isDisposed;
    private Task? _disposeTask;

    public GlobalShortcutCoordinator(
        IGlobalShortcutService shortcutService,
        MainWindowViewModel viewModel,
        ILogger? logger = null)
    {
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger;
    }

    public IReadOnlyList<GlobalShortcutRegistrationResult> Register(
        IEnumerable<GlobalShortcutDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        ObjectDisposedException.ThrowIf(_isDisposed, nameof(GlobalShortcutCoordinator));

        var results = new List<GlobalShortcutRegistrationResult>();
        foreach (var definition in definitions)
        {
            GlobalShortcutRegistrationResult result;

            try
            {
                result = _shortcutService.TryRegister(definition, () => Execute(definition.Id));
            }
            catch (Exception error)
            {
                if (_logger is not null)
                {
                    ShortcutLogMessages.RegistrationException(_logger, error, definition.Id);
                }

                result = new GlobalShortcutRegistrationResult(
                    definition.Id,
                    false,
                    error.Message);
            }

            results.Add(result);
            if (!result.IsRegistered && _logger is not null)
            {
                ShortcutLogMessages.RegistrationFailed(
                    _logger,
                    definition.Id,
                    definition.DisplayName,
                    result.ErrorMessage ?? "unknown error");
            }
        }

        return results;
    }

    public ValueTask DisposeAsync()
    {
        lock (_callbackGate)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            _isDisposed = true;
            UnregisterShortcuts();
            _disposeTask = DrainCallbacksAsync(_activeCallbackTasks.ToArray());
            return new ValueTask(_disposeTask);
        }
    }

    private void UnregisterShortcuts()
    {
        try
        {
            _shortcutService.UnregisterAll();
            _shortcutService.Dispose();
        }
        catch (Exception error)
        {
            if (_logger is not null)
            {
                ShortcutLogMessages.UnregisterServiceException(_logger, error);
            }
        }
    }

    private void Execute(string shortcutId)
    {
        lock (_callbackGate)
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                var command = GetCommand(shortcutId);
                if (command is null || !command.CanExecute(null))
                {
                    return;
                }

                command.Execute(null);
                if (command.ExecutionTask is not { } executionTask)
                {
                    return;
                }

                _activeCallbackTasks.Add(executionTask);
                _ = executionTask.ContinueWith(
                    completedTask => CallbackCompleted(completedTask),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception error)
            {
                if (_logger is not null)
                {
                    ShortcutLogMessages.ExecutionException(_logger, error, shortcutId);
                }
            }
        }
    }

    private IAsyncRelayCommand? GetCommand(string shortcutId)
        => shortcutId switch
        {
            GlobalShortcutDefaults.FullMuteId => _viewModel.ToggleFullMuteCommand,
            GlobalShortcutDefaults.FullOffId => _viewModel.ToggleFullOffCommand,
            _ => null,
        };

    private void CallbackCompleted(Task completedTask)
    {
        lock (_callbackGate)
        {
            _activeCallbackTasks.Remove(completedTask);
        }
    }

    private async Task DrainCallbacksAsync(IReadOnlyCollection<Task> callbackTasks)
    {
        try
        {
            await Task.WhenAll(callbackTasks).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            if (_logger is not null)
            {
                ShortcutLogMessages.DrainException(_logger, error);
            }
        }
        finally
        {
            lock (_callbackGate)
            {
                _activeCallbackTasks.Clear();
            }
        }
    }
}
