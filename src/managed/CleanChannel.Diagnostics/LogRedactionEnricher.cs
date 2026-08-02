using Serilog.Core;
using Serilog.Events;

namespace CleanChannel.Diagnostics;

internal sealed class LogRedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
        {
            var safeValue = RedactValue(property.Key, property.Value);
            logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, safeValue));
        }
    }

    private static LogEventPropertyValue RedactValue(
        string propertyName,
        LogEventPropertyValue value)
    {
        if (LogRedactor.IsSensitiveKey(propertyName))
        {
            return new ScalarValue(LogRedactor.RedactedValue);
        }

        return value switch
        {
            ScalarValue scalar when scalar.Value is string text
                => new ScalarValue(LogRedactor.Redact(text)),
            SequenceValue sequence
                => new SequenceValue(sequence.Elements.Select(element => RedactValue(propertyName, element))),
            StructureValue structure
                => new StructureValue(
                    structure.Properties.Select(property =>
                        new LogEventProperty(
                            property.Name,
                            RedactValue(property.Name, property.Value))),
                    structure.TypeTag),
            _ => value,
        };
    }
}
