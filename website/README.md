# CleanChannel Landing Demo

This directory contains a clearly local/demo-only ASP.NET Core landing and commerce sandbox. It demonstrates the intended open-core product boundary:

- The free CleanChannel core remains downloadable without an account.
- Pro messaging, account registration, purchase simulation, license activation, and artifact authorization live here as a separate control-plane demo.
- The public desktop app never contacts this site to unlock core audio.
- Accounts, licenses, and purchases are held in memory and disappear when the process stops.

## Run it

```powershell
dotnet run --project .\website\CleanChannel.Landing\CleanChannel.Landing.csproj
```

The app will print a local URL. Register a demo account, simulate an annual or lifetime purchase, activate the one-time code, and download the placeholder Pro artifact. The artifact is not a real signed Pro build.

Run tests with:

```powershell
dotnet test .\website\CleanChannel.Landing.Tests\CleanChannel.Landing.Tests.csproj
```

## Configuration

Configuration is read from `appsettings.json` and environment variables under `Landing__...`:

- `Landing__DemoMode` defaults to `true` and must remain visibly enabled until a real backend exists.
- `Landing__RequireHttps` defaults to secure behavior. The checked-in local sandbox explicitly sets `RequireHttps=false` together with `AllowHttpDemo=true`; HTTP is rejected for production/non-demo configuration.
- HTTP demo requests are accepted only on `localhost`; every non-Development environment, including Staging, requires HTTPS.
- `Landing__DataProtectionKeyRingPath` selects the writable Data Protection key ring. Startup fails if it cannot be created/probed, and `/health` returns `503` if it later becomes unwritable.
- `/health` also performs a real Data Protection Protect/Unprotect round trip. Demo logging uses the console provider explicitly so the local sandbox does not depend on a platform EventLog source.
- `Landing__EnableDemoAuthRateLimit` enables the bounded in-memory demo limiter. Production must disable it and provide a durable edge/API limiter instead.
- `Landing__AuthRateLimitPermitLimit`, `Landing__AuthRateLimitWindowSeconds`, and `Landing__AuthRateLimitMaxTrackedKeys` tune the local limiter.
- `Landing__LicensingBackendConfigured` and `Landing__ArtifactStoreConfigured` are required when `DemoMode=false`. The current site still returns fail-closed `503` responses because no real adapters are included.
- `Landing__ProArtifactPath` points to the demo artifact or, later, a signed artifact store.

## Production TODOs

- Replace the in-memory store with a durable database and migrations.
- Integrate Stripe (or another payment provider) using server-side checkout sessions and signed webhook verification; never accept payment success from the browser.
- Implement and wire real licensing and artifact-store adapters before setting `DemoMode=false`; the current in-memory purchase flow and placeholder artifact must never be used in that mode.
- Add email verification, password recovery, abuse controls, durable rate limiting, and account deletion/export workflows.
- Use a production identity/session system with key rotation, encrypted durable Data Protection keys, and a controlled key backup/restore process.
- Issue signed entitlements and bind downloads to authenticated, active entitlements.
- Store signed application/driver artifacts outside the web app, with malware scanning, immutable versioning, access logs, and CDN delivery.
- Publish legal purchase terms, refund policy, privacy notice, cookie notice, and a support/contact process.
- Require HTTPS, secure deployment configuration, monitoring, backups, and incident response.
