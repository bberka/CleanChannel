namespace CleanChannel.Diagnostics;

public static class LogRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitiveKeys =
    [
        "access_token",
        "accesstoken",
        "api_key",
        "apikey",
        "audio_samples",
        "audiosamples",
        "bearer",
        "authorization",
        "client_secret",
        "cookie",
        "cookies",
        "license",
        "license_key",
        "licensekey",
        "password",
        "pcm",
        "private_key",
        "refresh_token",
        "samples",
        "secret",
        "session",
        "set_cookie",
        "token",
    ];

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var redacted = RedactBearerValues(value);
        foreach (var key in SensitiveKeys)
        {
            redacted = RedactKeyValue(redacted, key);
        }

        return redacted;
    }

    internal static bool IsSensitiveKey(string key)
    {
        var normalized = key
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized is
            "accesstoken" or
            "apikey" or
            "audiosamples" or
            "authorization" or
            "bearer" or
            "bearertoken" or
            "clientsecret" or
            "cookie" or
            "cookies" or
            "license" or
            "licensekey" or
            "password" or
            "pcm" or
            "privatekey" or
            "refreshtoken" or
            "samples" or
            "secret" or
            "session" or
            "setcookie" or
            "token";
    }

    private static string RedactKeyValue(string value, string key)
    {
        var searchOffset = 0;
        while (searchOffset < value.Length)
        {
            var keyStart = value.IndexOf(key, searchOffset, StringComparison.OrdinalIgnoreCase);
            if (keyStart < 0)
            {
                return value;
            }

            var keyEnd = keyStart + key.Length;
            var hasBoundary = keyStart == 0
                || !char.IsLetterOrDigit(value[keyStart - 1]) && value[keyStart - 1] != '_';
            var separatorIndex = keyEnd;
            if (separatorIndex + 1 < value.Length
                && value[separatorIndex] == '"'
                && value[separatorIndex + 1] == ':')
            {
                separatorIndex++;
            }

            var hasSeparator = separatorIndex < value.Length
                && (value[separatorIndex] == '=' || value[separatorIndex] == ':');
            if (!hasBoundary || !hasSeparator)
            {
                searchOffset = keyEnd;
                continue;
            }

            var valueStart = separatorIndex + 1;
            while (valueStart < value.Length && char.IsWhiteSpace(value[valueStart]))
            {
                valueStart++;
            }

            var isQuoted = valueStart < value.Length && value[valueStart] == '"';
            if (isQuoted)
            {
                valueStart++;
            }

            var valueEnd = valueStart;
            if (isQuoted)
            {
                valueEnd = FindQuotedValueEnd(value, valueStart);
            }
            else
            {
                while (valueEnd < value.Length
                    && !char.IsWhiteSpace(value[valueEnd])
                    && value[valueEnd] != ','
                    && value[valueEnd] != ';'
                    && value[valueEnd] != '}')
                {
                    valueEnd++;
                }
            }

            value = string.Concat(
                value.AsSpan(0, valueStart),
                RedactedValue,
                value.AsSpan(valueEnd));
            searchOffset = valueStart + RedactedValue.Length;
        }

        return value;
    }

    private static int FindQuotedValueEnd(string value, int valueStart)
    {
        var escaped = false;
        for (var index = valueStart; index < value.Length; index++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (value[index] == '\\')
            {
                escaped = true;
            }
            else if (value[index] == '"')
            {
                return index;
            }
        }

        return value.Length;
    }

    private static string RedactBearerValues(string value)
    {
        var marker = "Bearer ";
        var searchOffset = 0;
        while (searchOffset < value.Length)
        {
            var markerIndex = value.IndexOf(marker, searchOffset, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return value;
            }

            var valueStart = markerIndex + marker.Length;
            var valueEnd = valueStart;
            while (valueEnd < value.Length && !char.IsWhiteSpace(value[valueEnd]))
            {
                valueEnd++;
            }

            value = string.Concat(
                value.AsSpan(0, valueStart),
                RedactedValue,
                value.AsSpan(valueEnd));
            searchOffset = valueStart + RedactedValue.Length;
        }

        return value;
    }
}
