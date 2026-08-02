using Serilog.Core;
using Serilog.Events;

namespace CleanChannel.Diagnostics;

internal sealed class EventIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("EventId", out var eventIdValue)
            || !TryReadEventId(eventIdValue, out var eventId, out var eventName))
        {
            return;
        }

        logEvent.AddOrUpdateProperty(new LogEventProperty("EventId", new ScalarValue(eventId)));
        if (!string.IsNullOrWhiteSpace(eventName))
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty("EventName", new ScalarValue(eventName)));
        }
    }

    private static bool TryReadEventId(
        LogEventPropertyValue value,
        out int eventId,
        out string? eventName)
    {
        eventId = 0;
        eventName = null;

        if (value is ScalarValue { Value: int scalarEventId })
        {
            eventId = scalarEventId;
            return true;
        }

        if (value is not StructureValue structure)
        {
            return false;
        }

        var foundId = false;
        foreach (var property in structure.Properties)
        {
            if (string.Equals(property.Name, "Id", StringComparison.Ordinal)
                && property.Value is ScalarValue { Value: int structuredEventId })
            {
                eventId = structuredEventId;
                foundId = true;
            }
            else if (string.Equals(property.Name, "Name", StringComparison.Ordinal)
                && property.Value is ScalarValue { Value: string structuredEventName })
            {
                eventName = structuredEventName;
            }
        }

        return foundId;
    }
}
