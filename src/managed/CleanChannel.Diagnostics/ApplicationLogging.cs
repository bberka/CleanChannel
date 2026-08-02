using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Display;
using Serilog.Formatting;

namespace CleanChannel.Diagnostics;

public sealed class ApplicationLogging : IDisposable
{
    private readonly Serilog.ILogger _serilogLogger;
    private int _disposed;

    internal ApplicationLogging(
        ILoggerFactory loggerFactory,
        Serilog.ILogger serilogLogger,
        string logDirectory)
    {
        LoggerFactory = loggerFactory;
        _serilogLogger = serilogLogger;
        LogDirectory = logDirectory;
    }

    public ILoggerFactory LoggerFactory { get; }

    public string LogDirectory { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            LoggerFactory.Dispose();
            (_serilogLogger as IDisposable)?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

public static class ApplicationLoggingFactory
{
    public static ApplicationLogging Create(ApplicationLoggingOptions? options = null)
    {
        options ??= new ApplicationLoggingOptions();
        options.Validate();

        var logDirectory = options.GetResolvedLogDirectory();
        Directory.CreateDirectory(logDirectory);

        var formatter = new RedactingTextFormatter();
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(ToSerilogLevel(options.MinimumLevel))
            .Enrich.FromLogContext()
            .Enrich.With<LogRedactionEnricher>()
            .Enrich.With<EventIdEnricher>()
            .Enrich.With<UtcTimestampEnricher>()
            .WriteTo.File(
                formatter,
                Path.Combine(logDirectory, "cleanchannel-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: options.MaxFileSizeBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: options.RetainedFileCount,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        if (options.EnableConsole)
        {
            configuration = configuration.WriteTo.Console(formatter);
        }

        var serilogLogger = configuration.CreateLogger();
        try
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(options.MinimumLevel);
                builder.AddSerilog(serilogLogger, dispose: false);
            });

            return new ApplicationLogging(loggerFactory, serilogLogger, logDirectory);
        }
        catch
        {
            serilogLogger.Dispose();
            throw;
        }
    }

    private static LogEventLevel ToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }
}

internal sealed class RedactingTextFormatter : ITextFormatter
{
    private readonly MessageTemplateTextFormatter _formatter = new(
        "{UtcTimestamp:O} [{Level}] [{SourceContext}] [EventId={EventId}] {Message:lj}{NewLine}{Exception}",
        CultureInfo.InvariantCulture);

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        _formatter.Format(logEvent, buffer);
        output.Write(LogRedactor.Redact(buffer.ToString()));
    }
}
