using System.Security.Claims;
using System.Net;
using CleanChannel.Landing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddConsole();
var landingOptions = builder.Configuration.GetSection("Landing").Get<LandingOptions>() ?? new LandingOptions();
LandingConfiguration.Validate(landingOptions, builder.Environment.EnvironmentName);
var keyRing = DataProtectionKeyRing.Initialize(landingOptions.DataProtectionKeyRingPath);

builder.Services.AddSingleton(landingOptions);
builder.Services.AddSingleton(keyRing);
builder.Services.AddSingleton<InMemoryDemoStore>();
builder.Services.AddSingleton<IDemoStore>(services => services.GetRequiredService<InMemoryDemoStore>());
builder.Services.AddSingleton<IPasswordHasher<DemoUser>, PasswordHasher<DemoUser>>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DemoLicenseService>();
builder.Services.AddSingleton<DownloadAuthorizationService>();
builder.Services.AddSingleton<DemoAuthRateLimiter>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRing.Path))
    .SetApplicationName("CleanChannel.Landing");
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "cleanchannel.demo.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = landingOptions.RequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "cleanchannel.demo.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = landingOptions.RequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/downloads", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (landingOptions.RequireHttps)
{
    app.UseHttpsRedirection();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
}

app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    if (landingOptions.RequireHttps)
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    if (!landingOptions.RequireHttps && !context.Request.IsHttps &&
        (!landingOptions.DemoMode || !landingOptions.AllowHttpDemo ||
         !context.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         (context.Connection.RemoteIpAddress is not null && !IPAddress.IsLoopback(context.Connection.RemoteIpAddress))))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("HTTP is available only for the explicit localhost demo.");
        return;
    }

    if (IsPrivateResponsePath(context.Request.Path))
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "private, no-store";
            return Task.CompletedTask;
        });
    }

    if (landingOptions.EnableDemoAuthRateLimit && IsAuthMutation(context.Request))
    {
        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!context.RequestServices.GetRequiredService<DemoAuthRateLimiter>().TryAccept(clientKey))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.CacheControl = "private, no-store";
            await context.Response.WriteAsync("Too many authentication attempts. Try again later.");
            return;
        }
    }

    await next(context);
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", (LandingOptions options, DataProtectionKeyRing keys, IDataProtectionProvider dataProtection) =>
{
    if (!options.DemoMode)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Licensing backend unavailable",
            detail: "DemoMode=false is fail-closed until private licensing and artifact-store adapters are wired.");
    }

    return keys.IsCryptographicallyUsable(dataProtection)
        ? Results.Ok(new { status = "demo", demoMode = options.DemoMode, dataProtection = "ready" })
        : Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Data Protection key ring unavailable",
            detail: "The configured key ring failed a Protect/Unprotect check; authentication cannot be trusted.");
});
app.MapGet("/downloads/pro", (HttpContext context, DownloadAuthorizationService authorization, LandingOptions options, IWebHostEnvironment environment) =>
{
    context.Response.Headers.CacheControl = "private, no-store";
    if (!options.DemoMode)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Pro artifact store unavailable",
            detail: "DemoMode=false is fail-closed until a real artifact store is wired.");
    }

    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var decision = authorization.Authorize(userId);
    if (decision.Decision == DownloadDecision.Unauthenticated)
    {
        return Results.Unauthorized();
    }

    if (!decision.Allowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var artifactPath = Path.IsPathRooted(options.ProArtifactPath)
        ? options.ProArtifactPath
        : Path.Combine(environment.ContentRootPath, options.ProArtifactPath);
    return File.Exists(artifactPath)
        ? Results.File(artifactPath, "application/octet-stream", "CleanChannel-Pro-Demo.txt")
        : Results.NotFound();
});

app.MapRazorPages();

app.Run();

static bool IsPrivateResponsePath(PathString path) =>
    path.StartsWithSegments("/account") || path.StartsWithSegments("/downloads/pro");

static bool IsAuthMutation(HttpRequest request) =>
    HttpMethods.IsPost(request.Method) &&
    (request.Path.StartsWithSegments("/account/login") ||
     request.Path.StartsWithSegments("/account/register") ||
     request.Path.Equals("/account", StringComparison.OrdinalIgnoreCase));

public partial class Program;
