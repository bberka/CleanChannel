using CleanChannel.Landing;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CleanChannel.Landing.Tests;

public sealed class LandingConfigurationTests
{
    [Fact]
    public void HttpRequiresExplicitDemoOptIn()
    {
        var options = new LandingOptions { RequireHttps = false, DemoMode = true, AllowHttpDemo = false };

        var exception = Assert.Throws<InvalidOperationException>(() => LandingConfiguration.Validate(options, "Development"));

        Assert.Contains("AllowHttpDemo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRequiresHttpsAndDurableRateLimitHook()
    {
        var options = new LandingOptions { RequireHttps = false, DemoMode = false, EnableDemoAuthRateLimit = true };

        var exception = Assert.Throws<InvalidOperationException>(() => LandingConfiguration.Validate(options, "Production"));

        Assert.Contains("RequireHttps", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StagingRequiresHttps()
    {
        var options = new LandingOptions { RequireHttps = false, DemoMode = true, AllowHttpDemo = true };

        var exception = Assert.Throws<InvalidOperationException>(() => LandingConfiguration.Validate(options, "Staging"));

        Assert.Contains("outside Development", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonDemoRequiresRealBackendConfiguration()
    {
        var options = new LandingOptions { RequireHttps = true, DemoMode = false, EnableDemoAuthRateLimit = false };

        var exception = Assert.Throws<InvalidOperationException>(() => LandingConfiguration.Validate(options, "Production"));

        Assert.Contains("licensing backend and artifact store", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyRingIsCreatedAndProbeIsRemoved()
    {
        var path = Path.Combine(Path.GetTempPath(), "cleanchannel-keyring-" + Guid.NewGuid().ToString("N"));
        try
        {
            var keyRing = DataProtectionKeyRing.Initialize(path);
            var provider = DataProtectionProvider.Create(
                new DirectoryInfo(path),
                configuration => configuration.SetApplicationName("CleanChannel.Landing"));

            Assert.True(keyRing.IsWritable());
            Assert.True(keyRing.IsCryptographicallyUsable(provider));
            Assert.Empty(Directory.EnumerateFiles(path, ".write-test-*"));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
