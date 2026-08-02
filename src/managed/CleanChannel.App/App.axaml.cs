using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CleanChannel.App.Shortcuts;
using CleanChannel.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CleanChannel.App;

public partial class App : Application, IDisposable
{
    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    private ShortcutHostWindow? _shortcutHostWindow;
    private GlobalShortcutCoordinator? _shortcutCoordinator;
    private bool _ownsSingleInstanceMutex;
    private bool _isExiting;
    private bool _shutdownComplete;
    private Task? _shutdownTask;
    private ApplicationLogging? _logging;
    private ILogger? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InitializeLogging();

            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: "CleanChannel.App.SingleInstance",
                createdNew: out var createdNew);

            if (!createdNew)
            {
                if (_logger is not null)
                {
                    AppLogMessages.SecondInstance(_logger);
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                desktop.Shutdown();
                return;
            }

            _ownsSingleInstanceMutex = true;

            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow.Closing += MainWindow_OnClosing;
            desktop.ShutdownRequested += Desktop_OnShutdownRequested;
            desktop.Exit += Desktop_OnExit;

            var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
            if (trayIcon is not null)
            {
                trayIcon.Icon = TrayIconFactory.Create();
            }

            if (_logger is not null)
            {
                AppLogMessages.Started(_logger);
            }
        }

        base.OnFrameworkInitializationCompleted();
        InitializeBackgroundServicesAsync();
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OpenMenuItem_OnClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void ExitMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.TryShutdown();
    }

    private async void FullMuteMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (_mainWindow is not null)
        {
            await _mainWindow.ViewModel.ToggleFullMuteAsync();
        }
    }

    private async void FullOffMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (_mainWindow is not null)
        {
            await _mainWindow.ViewModel.ToggleFullOffAsync();
        }
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private async void Desktop_OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_shutdownComplete)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownTask is not null)
        {
            return;
        }

        _isExiting = true;
        _shutdownTask = CompleteShutdownAsync();

        try
        {
            await _shutdownTask;
        }
        catch (Exception error)
        {
            if (_logger is not null)
            {
                AppLogMessages.ShutdownFailed(_logger, error);
            }
        }
    }

    private async Task CompleteShutdownAsync()
    {
        _mainWindow?.ViewModel.StopAcceptingOperations();
        await DisposeGlobalShortcutsAsync();

        if (_mainWindow is not null)
        {
            await _mainWindow.DisposeAsync();
            _mainWindow = null;
        }

        _shutdownComplete = true;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void Desktop_OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _shortcutHostWindow?.Close();
        _shortcutHostWindow = null;
        Dispose();
    }

    private async Task DisposeGlobalShortcutsAsync()
    {
        var coordinator = _shortcutCoordinator;
        _shortcutCoordinator = null;

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
        }

        _shortcutHostWindow?.Close();
        _shortcutHostWindow = null;
    }

    private async void InitializeBackgroundServicesAsync()
    {
        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            await _mainWindow.ViewModel.InitializeAsync();
            if (_isExiting)
            {
                return;
            }

            _shortcutHostWindow = new ShortcutHostWindow();
            _shortcutHostWindow.Show();

            _shortcutCoordinator = new GlobalShortcutCoordinator(
                new WindowsGlobalShortcutService(_shortcutHostWindow, _logger),
                _mainWindow.ViewModel,
                _logger);

            var results = _shortcutCoordinator.Register(GlobalShortcutDefaults.All);
            var registeredCount = results.Count(result => result.IsRegistered);
            if (_logger is not null && registeredCount > 0)
            {
                ShortcutLogMessages.Registered(_logger, registeredCount);
            }
        }
        catch (Exception error)
        {
            if (_logger is not null)
            {
                ShortcutLogMessages.InitializationException(_logger, error);
            }

            var coordinator = _shortcutCoordinator;
            _shortcutCoordinator = null;
            if (coordinator is not null)
            {
                await coordinator.DisposeAsync();
            }

            _shortcutHostWindow?.Close();
            _shortcutHostWindow = null;
        }
    }

    private void InitializeLogging()
    {
        try
        {
            _logging = ApplicationLoggingFactory.Create(new ApplicationLoggingOptions
            {
                EnableConsole = System.Diagnostics.Debugger.IsAttached,
            });
            _logger = _logging.LoggerFactory.CreateLogger<App>();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.WriteLine($"CleanChannel logging initialization failed: {error}");
        }
    }

    public void Dispose()
    {
        _shortcutHostWindow?.Close();
        _shortcutHostWindow = null;

        if (_logger is not null)
        {
            AppLogMessages.Stopped(_logger);
        }

        _logging?.Dispose();
        _logging = null;
        _logger = null;

        if (_singleInstanceMutex is null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex.ReleaseMutex();
            _ownsSingleInstanceMutex = false;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        GC.SuppressFinalize(this);
    }
}
