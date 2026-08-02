namespace CleanChannel.Landing;

public enum DemoPlan
{
    Annual,
    Lifetime,
}

public sealed class DemoUser
{
    public required string Id { get; init; }

    public required string Email { get; init; }

    public required string PasswordHash { get; set; }
}

public sealed class DemoLicense
{
    public required string Id { get; init; }

    public required string OwnerUserId { get; init; }

    public required DemoPlan Plan { get; init; }

    public required string ActivationCodeHash { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed record PurchaseReceipt(
    string LicenseId,
    DemoPlan Plan,
    string ActivationCode,
    DateTimeOffset IssuedAt);

public sealed record LicenseStatus(
    string? LicenseId,
    DemoPlan? Plan,
    bool IsActive,
    DateTimeOffset? ExpiresAt);

public enum DownloadDecision
{
    Allowed,
    Unauthenticated,
    NoActiveLicense,
}

public sealed record DownloadAuthorization(DownloadDecision Decision)
{
    public bool Allowed => Decision == DownloadDecision.Allowed;
}
