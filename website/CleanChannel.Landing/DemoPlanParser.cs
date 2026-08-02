namespace CleanChannel.Landing;

public static class DemoPlanParser
{
    public static bool TryParse(string? value, out DemoPlan plan)
    {
        switch (value?.Trim())
        {
            case string annual when annual.Equals(nameof(DemoPlan.Annual), StringComparison.OrdinalIgnoreCase):
                plan = DemoPlan.Annual;
                return true;
            case string lifetime when lifetime.Equals(nameof(DemoPlan.Lifetime), StringComparison.OrdinalIgnoreCase):
                plan = DemoPlan.Lifetime;
                return true;
            default:
                plan = default;
                return false;
        }
    }
}
