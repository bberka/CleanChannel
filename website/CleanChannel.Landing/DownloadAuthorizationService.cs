namespace CleanChannel.Landing;

public sealed class DownloadAuthorizationService
{
    private readonly DemoLicenseService _licenses;

    public DownloadAuthorizationService(DemoLicenseService licenses) => _licenses = licenses;

    public DownloadAuthorization Authorize(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new DownloadAuthorization(DownloadDecision.Unauthenticated);
        }

        return _licenses.GetStatus(userId).IsActive
            ? new DownloadAuthorization(DownloadDecision.Allowed)
            : new DownloadAuthorization(DownloadDecision.NoActiveLicense);
    }
}
