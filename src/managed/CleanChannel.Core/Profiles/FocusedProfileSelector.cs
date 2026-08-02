namespace CleanChannel.Core.Profiles;

public static class FocusedProfileSelector
{
    public static FocusedProfile? Select(
        string? foregroundProcessName,
        IEnumerable<FocusedProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        return profiles
            .Where(profile => profile.Matches(foregroundProcessName))
            .OrderByDescending(profile => profile.Priority)
            .ThenBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .ThenBy(profile => profile.DisplayName, StringComparer.Ordinal)
            .ThenBy(profile => profile.ProcessName, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
