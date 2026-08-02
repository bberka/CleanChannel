namespace CleanChannel.Landing;

public sealed class LandingOptions
{
    public bool DemoMode { get; set; } = true;

    public bool RequireHttps { get; set; } = true;

    public bool AllowHttpDemo { get; set; }

    public bool LicensingBackendConfigured { get; set; }

    public bool ArtifactStoreConfigured { get; set; }

    public bool EnableDemoAuthRateLimit { get; set; } = true;

    public int AuthRateLimitPermitLimit { get; set; } = 10;

    public int AuthRateLimitWindowSeconds { get; set; } = 60;

    public int AuthRateLimitMaxTrackedKeys { get; set; } = 1024;

    public string DataProtectionKeyRingPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CleanChannel",
        "LandingDemo",
        "keys");

    public string ProArtifactPath { get; set; } = "artifacts/CleanChannel-Pro-Demo.txt";
}
