namespace CleanChannel.Core.Profiles;

public sealed record FocusedProfile(
    string ProfileId,
    string DisplayName,
    string ProcessName,
    int Priority,
    bool IsEnabled = true)
{
    public bool Matches(string? foregroundProcessName)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var expectedProcessName = NormalizeProcessName(ProcessName);
        var actualProcessName = NormalizeProcessName(foregroundProcessName);

        return expectedProcessName is not null
            && actualProcessName is not null
            && string.Equals(
                expectedProcessName,
                actualProcessName,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
    }
}
