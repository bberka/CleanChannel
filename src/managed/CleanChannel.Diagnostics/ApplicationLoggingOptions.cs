using Microsoft.Extensions.Logging;

namespace CleanChannel.Diagnostics;

public sealed class ApplicationLoggingOptions
{
    public string? LogDirectory { get; init; }

    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    public int RetainedFileCount { get; init; } = 14;

    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    public bool EnableConsole { get; init; }

    public static string GetDefaultLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CleanChannel",
            "Logs");
    }

    internal string GetResolvedLogDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(LogDirectory)
            ? GetDefaultLogDirectory()
            : LogDirectory.Trim();

        return Path.GetFullPath(directory);
    }

    internal void Validate()
    {
        if (RetainedFileCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetainedFileCount),
                RetainedFileCount,
                "At least one log file must be retained.");
        }

        if (MaxFileSizeBytes < 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxFileSizeBytes),
                MaxFileSizeBytes,
                "The maximum log file size must be at least 64 bytes.");
        }
    }
}
