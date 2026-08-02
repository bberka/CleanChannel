using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CleanChannel.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CleanChannel.App;

public partial class App : Application, IDisposable
{
    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    private bool _ownsSingleInstanceMutex;
    private bool _isExiting;
    private ApplicationLogging? _logging;
    private ILogger? _logger;

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
        _isExiting = true;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
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

    private void Desktop_OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Dispose();
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
