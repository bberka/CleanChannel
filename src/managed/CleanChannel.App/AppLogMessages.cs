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
}
