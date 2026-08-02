using CleanChannel.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable CA1707, CA1848, CA1873

namespace CleanChannel.Diagnostics.Tests;

public sealed class ApplicationLoggingTests
{
    [Fact]
    public void DefaultLogDirectoryIsUnderLocalApplicationData()
    {
        var expected = Path.GetFullPath(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CleanChannel",
                "Logs"));

        Assert.Equal(expected, ApplicationLoggingOptions.GetDefaultLogDirectory());
    }

    [Fact]
    public void LogDirectoryOverrideIsExposedAndCreated()
    {
        using var temporaryDirectory = TemporaryDirectory.Create();
        var logDirectory = Path.Combine(temporaryDirectory.Path, "custom-logs");

        using var logging = ApplicationLoggingFactory.Create(new ApplicationLoggingOptions
        {
            LogDirectory = logDirectory,
        });

        Assert.Equal(Path.GetFullPath(logDirectory), logging.LogDirectory);
        Assert.True(Directory.Exists(logDirectory));
    }

    [Fact]
    public void MinimumLevelFiltersLowerSeverityRecords()
    {
        using var temporaryDirectory = TemporaryDirectory.Create();
        using var logging = ApplicationLoggingFactory.Create(new ApplicationLoggingOptions
        {
            LogDirectory = temporaryDirectory.Path,
            MinimumLevel = LogLevel.Warning,
        });

        var logger = logging.LoggerFactory.CreateLogger("CleanChannel.Tests.Filtering");
        logger.LogInformation(new EventId(1, "Ignored"), "information should be filtered");
        logger.LogWarning(new EventId(2, "Warning"), "warning should be written");
        logger.LogError(new EventId(3, "Error"), new InvalidOperationException("failure"), "error should be written");

        logging.Dispose();
        var contents = ReadAllLogs(temporaryDirectory.Path);

        Assert.DoesNotContain("information should be filtered", contents, StringComparison.Ordinal);
        Assert.Contains("Warning", contents, StringComparison.Ordinal);
        Assert.Contains("Error", contents, StringComparison.Ordinal);
        Assert.Contains("EventId=3", contents, StringComparison.Ordinal);
        Assert.Contains("Z [Warning]", contents, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: failure", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyAndSizeRolloverKeepFilesBounded()
    {
        using var temporaryDirectory = TemporaryDirectory.Create();

        using var logging = ApplicationLoggingFactory.Create(new ApplicationLoggingOptions
        {
            LogDirectory = temporaryDirectory.Path,
            MinimumLevel = LogLevel.Trace,
            RetainedFileCount = 2,
            MaxFileSizeBytes = 1_024,
        });

        var logger = logging.LoggerFactory.CreateLogger("CleanChannel.Tests.Rolling");
        for (var index = 0; index < 12; index++)
        {
            logger.LogInformation(new EventId(10, "Entry"), "first-day-entry-{Index}-with-padding-0123456789", index);
        }

        logging.Dispose();
        var files = Directory.GetFiles(temporaryDirectory.Path, "*.log");

        Assert.InRange(files.Length, 1, 2);
        // The file sink rolls before the next event; the event that crosses the
        // threshold is retained in the previous file, so allow one event of headroom.
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 2_048));
        Assert.Contains(files, file => File.ReadAllText(file).Contains("first-day-entry-11", StringComparison.Ordinal));
    }

    [Fact]
    public void SensitiveStructuredValuesAndCommonSecretTextAreRedacted()
    {
        using var temporaryDirectory = TemporaryDirectory.Create();
        using var logging = ApplicationLoggingFactory.Create(new ApplicationLoggingOptions
        {
            LogDirectory = temporaryDirectory.Path,
        });

        var logger = logging.LoggerFactory.CreateLogger("CleanChannel.Tests.Redaction");
        logger.LogInformation(
            new EventId(20, "SensitiveValues"),
            "device={DeviceId} access_token={AccessToken} license={LicenseKey} samples={AudioSamples} bearer=Bearer abc123 ignored={Ignored}",
            "device-1",
            "access-token-value",
            "license-key-value",
            "pcm-sample-data",
            "ignored");
        logger.LogInformation("Using {AccessToken}", "access-token-value");

        logging.Dispose();
        var contents = ReadAllLogs(temporaryDirectory.Path);

        Assert.Contains("device-1", contents, StringComparison.Ordinal);
        Assert.Contains(LogRedactor.RedactedValue, contents, StringComparison.Ordinal);
        Assert.DoesNotContain("access-token-value", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("license-key-value", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("pcm-sample-data", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapedQuotedSecretTextIsRedactedAsOneValue()
    {
        var value = "token=\"secret\\\"suffix\" other=value";

        var redacted = LogRedactor.Redact(value);

        Assert.Equal("token=\"[REDACTED]\" other=value", redacted);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("suffix", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void AdditionalCredentialFieldsAreRedacted()
    {
        var value = "authorization=basic client_secret=client refresh_token=refresh cookie=session private_key=key";

        var redacted = LogRedactor.Redact(value);

        Assert.DoesNotContain("authorization=basic", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret=client", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh_token=refresh", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("cookie=session", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("private_key=key", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void ConcurrentWritesRemainCompleteAndLineDelimited()
    {
        using var temporaryDirectory = TemporaryDirectory.Create();
        using var logging = ApplicationLoggingFactory.Create(new ApplicationLoggingOptions
        {
            LogDirectory = temporaryDirectory.Path,
            MinimumLevel = LogLevel.Trace,
            MaxFileSizeBytes = 1_000_000,
        });

        var logger = logging.LoggerFactory.CreateLogger("CleanChannel.Tests.Concurrent");
        Parallel.For(0, 100, index =>
        {
            logger.LogInformation(new EventId(30, "Concurrent"), "concurrent-entry-{Index}", index);
        });

        logging.Dispose();
        var lines = ReadAllLogs(temporaryDirectory.Path)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(100, lines.Length);
    }

    private static string ReadAllLogs(string directory)
    {
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.log")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CleanChannel.Diagnostics.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

#pragma warning restore CA1707, CA1848, CA1873
