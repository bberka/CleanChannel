using CleanChannel.Landing;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace CleanChannel.Landing.Tests;

public sealed class DemoCommerceTests
{
    [Fact]
    public void UnauthenticatedDownloadIsDenied()
    {
        var service = CreateServices(out _);

        var decision = service.Downloads.Authorize(null);

        Assert.Equal(DownloadDecision.Unauthenticated, decision.Decision);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void AuthenticatedUserWithoutLicenseIsDenied()
    {
        var service = CreateServices(out var user);

        var decision = service.Downloads.Authorize(user.Id);

        Assert.Equal(DownloadDecision.NoActiveLicense, decision.Decision);
    }

    [Fact]
    public void ActivatedLicenseAllowsDownload()
    {
        var service = CreateServices(out var user);
        var receipt = service.Licenses.IssuePurchase(user.Id, DemoPlan.Lifetime)!;

        Assert.True(service.Licenses.Activate(user.Id, receipt.ActivationCode));
        Assert.True(service.Downloads.Authorize(user.Id).Allowed);
    }

    [Fact]
    public void AnnualLicenseExpiresButLifetimeLicenseDoesNot()
    {
        var service = CreateServices(out var annualUser);
        var annual = service.Licenses.IssuePurchase(annualUser.Id, DemoPlan.Annual)!;
        Assert.True(service.Licenses.Activate(annualUser.Id, annual.ActivationCode));

        service.Clock.Advance(TimeSpan.FromDays(365));
        Assert.False(service.Licenses.GetStatus(annualUser.Id).IsActive);

        var lifetimeUser = service.Licenses.Register("lifetime@example.test", "correct horse battery staple")!;
        var lifetime = service.Licenses.IssuePurchase(lifetimeUser.Id, DemoPlan.Lifetime)!;
        Assert.True(service.Licenses.Activate(lifetimeUser.Id, lifetime.ActivationCode));
        service.Clock.Advance(TimeSpan.FromDays(3650));
        Assert.True(service.Licenses.GetStatus(lifetimeUser.Id).IsActive);
    }

    [Fact]
    public void ActivationCodeIsHashedAndInvalidCodesDoNotActivate()
    {
        var service = CreateServices(out var user);
        var receipt = service.Licenses.IssuePurchase(user.Id, DemoPlan.Annual)!;
        var stored = service.Store.FindLicenseById(receipt.LicenseId)!;

        Assert.NotEqual(receipt.ActivationCode, stored.ActivationCodeHash);
        Assert.Equal(64, stored.ActivationCodeHash.Length);
        Assert.False(service.Licenses.Activate(user.Id, "not-the-code"));
        Assert.False(service.Licenses.Activate(user.Id, new string('A', DemoLicenseService.ActivationCodeLength + 1)));
        Assert.False(service.Licenses.Activate(user.Id, new string('Z', DemoLicenseService.ActivationCodeLength)));
    }

    [Fact]
    public void PlanParserRejectsNumericAndUnknownValues()
    {
        Assert.False(DemoPlanParser.TryParse("0", out _));
        Assert.False(DemoPlanParser.TryParse("Premium", out _));
        Assert.True(DemoPlanParser.TryParse("lifetime", out var lifetime));
        Assert.Equal(DemoPlan.Lifetime, lifetime);
        Assert.Null(CreateServices(out var user).Licenses.IssuePurchase(user.Id, (DemoPlan)99));
    }

    [Fact]
    public void DemoAuthLimiterIsBoundedAndResetsAfterWindow()
    {
        var clock = new MutableClock(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var options = new LandingOptions { AuthRateLimitPermitLimit = 2, AuthRateLimitWindowSeconds = 60, AuthRateLimitMaxTrackedKeys = 1 };
        var limiter = new DemoAuthRateLimiter(options, clock);

        Assert.True(limiter.TryAccept("first"));
        Assert.True(limiter.TryAccept("first"));
        Assert.False(limiter.TryAccept("first"));
        Assert.True(limiter.TryAccept("second"));
        Assert.True(limiter.TryAccept("third"));
        Assert.False(limiter.TryAccept("fourth"));
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(limiter.TryAccept("first"));
    }

    private static TestServices CreateServices(out DemoUser user)
    {
        var store = new InMemoryDemoStore();
        var clock = new MutableClock(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var licenses = new DemoLicenseService(store, new PasswordHasher<DemoUser>(), clock, new LandingOptions());
        user = licenses.Register("demo@example.test", "correct horse battery staple")!;
        return new TestServices(store, clock, licenses, new DownloadAuthorizationService(licenses));
    }

    private sealed record TestServices(
        InMemoryDemoStore Store,
        MutableClock Clock,
        DemoLicenseService Licenses,
        DownloadAuthorizationService Downloads);

    private sealed class MutableClock(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;

        public override DateTimeOffset GetUtcNow() => _current;

        public void Advance(TimeSpan duration) => _current += duration;
    }
}
