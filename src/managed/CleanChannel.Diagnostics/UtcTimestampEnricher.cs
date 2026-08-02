using Serilog.Core;
using Serilog.Events;

namespace CleanChannel.Diagnostics;

internal sealed class UtcTimestampEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddOrUpdateProperty(
            new LogEventProperty(
                "UtcTimestamp",
                new ScalarValue(logEvent.Timestamp.UtcDateTime)));
    }
}
