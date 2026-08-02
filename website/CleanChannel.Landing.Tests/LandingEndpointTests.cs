using System.Net;
using System.Text.RegularExpressions;
using CleanChannel.Landing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CleanChannel.Landing.Tests;

public sealed class LandingEndpointTests
{
    [Fact]
    public async Task RegistrationPurchaseActivationDownloadAndLogoutFlowWorks()
    {
        using var factory = new LandingWebApplicationFactory(requireHttps: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var unauthenticatedDownload = await client.GetAsync("/downloads/pro");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedDownload.StatusCode);
        AssertPrivateNoStore(unauthenticatedDownload);

        var registerToken = await GetTokenAsync(client, "/account/register");
        var registration = await PostAsync(client, "/account/register", registerToken, new Dictionary<string, string>
        {
            ["Input.Email"] = "flow@example.test",
            ["Input.Password"] = "correct horse battery staple",
        });
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        Assert.Contains("cleanchannel.demo.auth", GetSetCookieHeaders(registration), StringComparison.OrdinalIgnoreCase);

        var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        AssertPrivateNoStore(account);
        var accountHtml = await account.Content.ReadAsStringAsync();
        Assert.Contains("No active Pro license", accountHtml, StringComparison.Ordinal);

        var purchase = await PostAsync(client, "/account?handler=Purchase", ExtractToken(accountHtml), new Dictionary<string, string>
        {
            ["plan"] = "Annual",
        });
        var purchaseHtml = await purchase.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, purchase.StatusCode);
        Assert.Contains("Demo Annual purchase issued", purchaseHtml, StringComparison.Ordinal);
        var activationCode = Regex.Match(purchaseHtml, "<code>([A-F0-9]{32})</code>").Groups[1].Value;
        Assert.Equal(DemoLicenseService.ActivationCodeLength, activationCode.Length);

        var activation = await PostAsync(client, "/account?handler=Activate", ExtractToken(purchaseHtml), new Dictionary<string, string>
        {
            ["activationCode"] = activationCode,
        });
        var activatedHtml = await activation.Content.ReadAsStringAsync();
        Assert.Contains("License activated", activatedHtml, StringComparison.Ordinal);

        var download = await client.GetAsync("/downloads/pro");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        AssertPrivateNoStore(download);
        Assert.Contains("placeholder", await download.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var logout = await PostAsync(client, "/account?handler=Logout", ExtractToken(activatedHtml), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var afterLogout = await client.GetAsync("/downloads/pro");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

        var loginToken = await GetTokenAsync(client, "/account/login");
        var login = await PostAsync(client, "/account/login", loginToken, new Dictionary<string, string>
        {
            ["Input.Email"] = "flow@example.test",
            ["Input.Password"] = "correct horse battery staple",
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/downloads/pro")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUnlicensedUserIsDeniedAndAuthRequestsAreCachedPrivately()
    {
        using var factory = new LandingWebApplicationFactory(requireHttps: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AssertPrivateNoStore(login);
        var registration = await PostAsync(client, "/account/register", ExtractToken(await login.Content.ReadAsStringAsync()), new Dictionary<string, string>
        {
            ["Input.Email"] = "unlicensed@example.test",
            ["Input.Password"] = "correct horse battery staple",
        });

        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        var denied = await client.GetAsync("/downloads/pro");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        AssertPrivateNoStore(denied);
    }

    [Fact]
    public async Task SecureConfigurationSetsHttpsCookiesHstsAndSecurityHeaders()
    {
        using var factory = new LandingWebApplicationFactory(requireHttps: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        var login = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("max-age=", GetHeader(login, "Strict-Transport-Security"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default-src 'self'", GetHeader(login, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.Equal("DENY", GetHeader(login, "X-Frame-Options"));
        AssertPrivateNoStore(login);

        var registration = await PostAsync(client, "/account/register", ExtractToken(await login.Content.ReadAsStringAsync()), new Dictionary<string, string>
        {
            ["Input.Email"] = "secure@example.test",
            ["Input.Password"] = "correct horse battery staple",
        });
        var cookies = GetSetCookieHeaders(registration);
        Assert.Contains("cleanchannel.demo.auth", cookies, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", cookies, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly", cookies, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", cookies, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthReportsKeyRingFailureClearly()
    {
        using var factory = new LandingWebApplicationFactory(requireHttps: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var healthy = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Contains("dataProtection", await healthy.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Directory.Delete(factory.KeyRingPath, recursive: true);
        var unhealthy = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unhealthy.StatusCode);
        Assert.Contains("key ring unavailable", await unhealthy.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FreshKeyDirectoryRendersLoginAndRegisterPages()
    {
        using var factory = new LandingWebApplicationFactory(requireHttps: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await client.GetAsync("/account/login");
        var register = await client.GetAsync("/account/register");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Contains("__RequestVerificationToken", await login.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", await register.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(Directory.EnumerateFiles(factory.KeyRingPath, "*.xml").Any());
    }

    [Fact]
    public async Task NonDemoModeFailsHealthAndRefusesSimulatedServices()
    {
        using var factory = new LandingWebApplicationFactory(requireHttps: true, demoMode: false, configureBackends: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        var health = await client.GetAsync("/health");
        var download = await client.GetAsync("/downloads/pro");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Contains("fail-closed", await health.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, download.StatusCode);
        Assert.Contains("artifact store", await download.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GetTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return ExtractToken(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string token, IDictionary<string, string> values)
    {
        var form = new Dictionary<string, string>(values) { ["__RequestVerificationToken"] = token };
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    private static string ExtractToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        return match.Success ? WebUtility.UrlDecode(match.Groups[1].Value) : throw new InvalidOperationException("Antiforgery token was not rendered.");
    }

    private static string GetHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : string.Empty;

    private static void AssertPrivateNoStore(HttpResponseMessage response)
    {
        var cacheControl = GetHeader(response, "Cache-Control");
        Assert.Contains("private", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", cacheControl, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join("\n", values) : string.Empty;

    private sealed class LandingWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly bool _requireHttps;
        private readonly bool _demoMode;
        private readonly bool _configureBackends;

        public LandingWebApplicationFactory(bool requireHttps, bool demoMode = true, bool configureBackends = false)
        {
            _requireHttps = requireHttps;
            _demoMode = demoMode;
            _configureBackends = configureBackends;
        }

        public string KeyRingPath { get; } = Path.Combine(Path.GetTempPath(), "cleanchannel-landing-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_requireHttps ? "Production" : "Development");
            builder.UseSetting("Landing:DemoMode", "true");
            builder.UseSetting("Landing:DemoMode", _demoMode.ToString());
            builder.UseSetting("Landing:RequireHttps", _requireHttps.ToString());
            builder.UseSetting("Landing:AllowHttpDemo", (!_requireHttps && _demoMode).ToString());
            builder.UseSetting("Landing:EnableDemoAuthRateLimit", (!_requireHttps && _demoMode).ToString());
            builder.UseSetting("Landing:LicensingBackendConfigured", _configureBackends.ToString());
            builder.UseSetting("Landing:ArtifactStoreConfigured", _configureBackends.ToString());
            builder.UseSetting("Landing:DataProtectionKeyRingPath", KeyRingPath);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Landing:DemoMode"] = _demoMode.ToString(),
                ["Landing:RequireHttps"] = _requireHttps.ToString(),
                ["Landing:AllowHttpDemo"] = (!_requireHttps && _demoMode).ToString(),
                ["Landing:EnableDemoAuthRateLimit"] = (!_requireHttps && _demoMode).ToString(),
                ["Landing:LicensingBackendConfigured"] = _configureBackends.ToString(),
                ["Landing:ArtifactStoreConfigured"] = _configureBackends.ToString(),
                ["Landing:DataProtectionKeyRingPath"] = KeyRingPath,
            }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(KeyRingPath))
            {
                Directory.Delete(KeyRingPath, recursive: true);
            }
        }
    }
}
