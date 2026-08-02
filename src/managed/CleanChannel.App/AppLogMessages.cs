using Microsoft.Extensions.Logging;

namespace CleanChannel.App;

internal static partial class AppLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "CleanChannel application started.")]
    public static partial void Started(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "CleanChannel application stopped.")]
    public static partial void Stopped(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "A second CleanChannel instance was requested; the new instance is exiting.")]
    public static partial void SecondInstance(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "CleanChannel shutdown coordination failed.")]
    public static partial void ShutdownFailed(ILogger logger, Exception exception);
}
