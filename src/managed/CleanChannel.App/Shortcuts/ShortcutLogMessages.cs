using Microsoft.Extensions.Logging;

namespace CleanChannel.App.Shortcuts;

internal static partial class ShortcutLogMessages
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Warning,
        Message = "Global shortcut {ShortcutId} ({DisplayName}) was not registered: {ErrorMessage}")]
    public static partial void RegistrationFailed(
        ILogger logger,
        string shortcutId,
        string displayName,
        string errorMessage);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Global shortcut {ShortcutId} registration threw an exception.")]
    public static partial void RegistrationException(
        ILogger logger,
        Exception exception,
        string shortcutId);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Error,
        Message = "Global shortcut {ShortcutId} could not execute.")]
    public static partial void ExecutionException(
        ILogger logger,
        Exception exception,
        string shortcutId);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Warning,
        Message = "Global shortcut {ShortcutId} could not be registered by Windows.")]
    public static partial void WindowsRegistrationException(
        ILogger logger,
        Exception exception,
        string shortcutId);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Warning,
        Message = "Global shortcut with native ID {NativeId} could not be unregistered.")]
    public static partial void UnregisterException(
        ILogger logger,
        Exception exception,
        int nativeId);

    [LoggerMessage(
        EventId = 1105,
        Level = LogLevel.Debug,
        Message = "The Avalonia Win32 shortcut hook was already unavailable during cleanup.")]
    public static partial void HookCleanupException(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1109,
        Level = LogLevel.Warning,
        Message = "Global shortcut service cleanup threw an exception.")]
    public static partial void UnregisterServiceException(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1106,
        Level = LogLevel.Error,
        Message = "Global shortcut {ShortcutId} callback failed.")]
    public static partial void CallbackException(
        ILogger logger,
        Exception exception,
        string shortcutId);

    [LoggerMessage(
        EventId = 1107,
        Level = LogLevel.Information,
        Message = "Registered {RegisteredCount} CleanChannel global shortcut(s).")]
    public static partial void Registered(
        ILogger logger,
        int registeredCount);

    [LoggerMessage(
        EventId = 1108,
        Level = LogLevel.Error,
        Message = "CleanChannel global shortcuts could not be initialized.")]
    public static partial void InitializationException(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 1110,
        Level = LogLevel.Warning,
        Message = "One or more global shortcut command tasks failed while draining during shutdown.")]
    public static partial void DrainException(
        ILogger logger,
        Exception exception);
}
