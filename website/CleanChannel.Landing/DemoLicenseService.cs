using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace CleanChannel.Landing;

public sealed class DemoLicenseService
{
    public const int ActivationCodeLength = 32;
    private static readonly TimeSpan AnnualDuration = TimeSpan.FromDays(365);
    private readonly IDemoStore _store;
    private readonly IPasswordHasher<DemoUser> _passwordHasher;
    private readonly TimeProvider _clock;
    private readonly LandingOptions _options;

    public DemoLicenseService(
        IDemoStore store,
        IPasswordHasher<DemoUser> passwordHasher,
        TimeProvider clock,
        LandingOptions options)
    {
        _store = store;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _options = options;
    }

    public DemoUser? Register(string email, string password)
    {
        if (!_options.DemoMode)
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null || password.Length is < 12 or > 128 || _store.FindUserByEmail(normalizedEmail) is not null)
        {
            return null;
        }

        var user = new DemoUser { Id = Guid.NewGuid().ToString("N"), Email = normalizedEmail, PasswordHash = string.Empty };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        return _store.TryAddUser(user) ? user : null;
    }

    public DemoUser? ValidateCredentials(string email, string password)
    {
        if (!_options.DemoMode)
        {
            return null;
        }

        var user = _store.FindUserByEmail(NormalizeEmail(email) ?? string.Empty);
        if (user is null || password.Length == 0)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded ? user : null;
    }

    public PurchaseReceipt? IssuePurchase(string userId, DemoPlan plan)
    {
        if (!_options.DemoMode || _store.FindUserById(userId) is null || !Enum.IsDefined(plan))
        {
            return null;
        }

        var code = CreateActivationCode();
        var now = _clock.GetUtcNow();
        var license = new DemoLicense
        {
            Id = $"demo-{Guid.NewGuid():N}",
            OwnerUserId = userId,
            Plan = plan,
            ActivationCodeHash = HashCode(code),
            IssuedAt = now,
        };
        _store.AddLicense(license);
        return new PurchaseReceipt(license.Id, plan, code, now);
    }

    public bool Activate(string userId, string? activationCode)
    {
        if (!_options.DemoMode)
        {
            return false;
        }

        var normalizedCode = activationCode?.Trim() ?? string.Empty;
        if (!IsValidActivationCode(normalizedCode))
        {
            return false;
        }

        var license = _store.FindLicensesForUser(userId)
            .FirstOrDefault(candidate => CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(candidate.ActivationCodeHash),
                Convert.FromHexString(HashCode(normalizedCode))));
        if (license is null)
        {
            return false;
        }

        var now = _clock.GetUtcNow();
        license.ActivatedAt ??= now;
        license.ExpiresAt = license.Plan == DemoPlan.Annual ? license.ActivatedAt.Value + AnnualDuration : null;
        return true;
    }

    public LicenseStatus GetStatus(string userId)
    {
        var license = _store.FindLicensesForUser(userId)
            .Where(candidate => candidate.ActivatedAt is not null)
            .OrderByDescending(candidate => candidate.ActivatedAt)
            .FirstOrDefault();
        var active = license is not null && (license.ExpiresAt is null || license.ExpiresAt > _clock.GetUtcNow());
        return new LicenseStatus(license?.Id, license?.Plan, active, license?.ExpiresAt);
    }

    private static string? NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return normalized.Length is >= 3 and <= 320 && normalized.Contains('@', StringComparison.Ordinal) ? normalized : null;
    }

    private static string CreateActivationCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public static bool IsValidActivationCode(string? activationCode) =>
        activationCode is { Length: ActivationCodeLength } && activationCode.All(Uri.IsHexDigit);

    private static string HashCode(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
