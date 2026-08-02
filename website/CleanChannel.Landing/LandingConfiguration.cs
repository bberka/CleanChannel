namespace CleanChannel.Landing;

public static class LandingConfiguration
{
    public static void Validate(LandingOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DataProtectionKeyRingPath))
        {
            throw new InvalidOperationException("Landing Data Protection key ring path must be configured.");
        }

        if (options.AuthRateLimitPermitLimit < 1 || options.AuthRateLimitWindowSeconds < 1 || options.AuthRateLimitMaxTrackedKeys < 1)
        {
            throw new InvalidOperationException("Landing auth rate-limit settings must be positive.");
        }

        if (string.IsNullOrWhiteSpace(environmentName) || !environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!options.RequireHttps)
            {
                throw new InvalidOperationException("Landing requires RequireHttps=true outside Development.");
            }

            if (options.EnableDemoAuthRateLimit)
            {
                throw new InvalidOperationException("The in-memory demo auth limiter must be disabled in production; configure a durable edge/API limiter instead.");
            }
        }

        if (!options.RequireHttps && (!options.DemoMode || !options.AllowHttpDemo))
        {
            throw new InvalidOperationException("HTTP is available only when both DemoMode=true and AllowHttpDemo=true.");
        }

        if (!options.DemoMode && (!options.LicensingBackendConfigured || !options.ArtifactStoreConfigured))
        {
            throw new InvalidOperationException("DemoMode=false requires a configured licensing backend and artifact store; the in-memory demo services are disabled.");
        }
    }
}
