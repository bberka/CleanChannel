# CleanChannel Architecture

## Scope

CleanChannel is a Windows-only product initially. Cross-platform UI support is not a goal until the Windows audio path is reliable.

The architecture separates the control plane, the real-time audio plane, and the virtual endpoint layer. This allows the UI and product features to evolve without putting UI work, licensing, or network dependencies inside the real-time audio callback.

## Components

### CleanChannel App

The C#/.NET 10 Avalonia application owns:

- Main settings UI.
- Tray icon and quick controls.
- Global shortcut registration.
- Profile editing.
- Application and channel verification messages.
- Startup behavior and user-facing recovery actions.
- Local configuration and licensing UI.

Avalonia supports Windows tray integration and NativeAOT publishing. NativeAOT should be enabled after the control plane is stable; reflection-heavy controls and dynamic loading must be avoided or explicitly configured.

References: [Avalonia TrayIcon](https://docs.avaloniaui.net/controls/navigation/trayicon), [Avalonia NativeAOT](https://docs.avaloniaui.net/docs/deployment/native-aot), [.NET NativeAOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

### Control plane

The control plane contains configuration, profile evaluation, channel state, licensing state, and health information. It communicates with the audio host through a versioned local IPC contract.

The UI must not own the real-time audio loop.

### Diagnostics

The managed control plane uses `CleanChannel.Diagnostics` for structured local logging through Serilog and the `Microsoft.Extensions.Logging` bridge. Logs default to `%LOCALAPPDATA%\CleanChannel\Logs`, use UTC timestamps and stable event identifiers, and are bounded by daily/size rollover and retention. Console output is opt-in for development. The logger redacts common credential, entitlement, and audio-sample fields; audio callbacks must not call it.

### AudioHost

The Rust user-mode process owns:

- WASAPI render and capture clients.
- MMDevice enumeration and endpoint notifications.
- Channel mixing and routing.
- Volume, mute, gain, and metering.
- EQ and other deterministic DSP.
- Microphone processing and sidetone.
- Device-loss recovery.
- Optional Pro processing supplied through an isolated extension boundary.

Audio callbacks must avoid allocations, blocking operations, logging, network access, and license checks.

### Virtual endpoint layer

Full mode uses a Windows virtual audio driver to expose stable render and capture endpoints. The driver is an audio transport boundary, not the home of product policy or complex DSP.

The initial endpoint pool should be finite and stable. A practical first pool is:

- `CleanChannel Sound`.
- `CleanChannel Chat`.
- `CleanChannel Game`.
- A small number of additional channel slots.
- `CleanChannel Mic`.

User-created channels should consume available slots rather than trigger driver installation/removal on every profile change.

References: [Microsoft virtual audio devices](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/virtual-audio-devices), [SysVAD sample](https://learn.microsoft.com/en-us/samples/microsoft/windows-driver-samples/sysvad-virtual-audio-device-driver-sample/)

### Pro extension boundary

Pro functionality should be delivered as a separate process rather than a reflection-loaded assembly inside the NativeAOT UI. The public app references only the source-generated `CleanChannel.ProContracts` assembly; the private `CleanChannel.ProHost` implementation is built from a separate ignored checkout and will communicate through versioned, authenticated local IPC. The current bootstrap defines the attestation validator but does not connect to a Pro host yet.

Candidate Pro modules include:

- AI noise cancellation.
- Premium processing models.
- Cloud profile synchronization.
- Premium preset catalog.
- Commercial integrations.

If a Pro module is unavailable, the core audio path must continue with a clear degraded-state message.

## Audio flows

### Render flow

```text
Application
  -> CleanChannel virtual render endpoint
  -> AudioHost channel bus
  -> volume / gain / EQ / optional Pro effects
  -> selected physical render endpoint
```

### Microphone flow

```text
Physical microphone
  -> AudioHost capture chain
  -> gain / EQ / gate / noise reduction / optional AI processing
  -> CleanChannel virtual microphone endpoint
  -> chat or recording application
```

The sidetone tap is taken from the microphone chain and routed to a selected physical output with an explicit delay/feedback warning.

### Lite-mode flow

Lite mode operates only on physical Windows endpoints and user-mode processing. It can prove the mixer, DSP, profiles, device switching, and UI without providing application-visible CleanChannel endpoints.

## Channel and profile model

```text
ChannelSlot
  - stable endpoint identity
  - display name
  - active policy
  - selected physical output
  - volume and mute state
  - effect chain

Profile
  - trigger rules
  - channel slot
  - EQ preset
  - processing settings
  - output route
  - activation and restore policy
```

Profile activation changes the CleanChannel graph. It does not need to force an application to select an endpoint.

## Application verification

When a profile trigger is active, CleanChannel should inspect the application's audio session and report one of these states:

- `NotRunning` — no matching process is active.
- `WaitingForAudio` — the process is active but has not created a usable audio session.
- `Configured` — the session is using the expected CleanChannel endpoint.
- `WrongEndpoint` — the session is using another endpoint.
- `EndpointUnavailable` — the expected channel slot is not available.
- `Unknown` — Windows returned insufficient information to make a safe assertion.

The UI should offer an explanation and a user action such as opening instructions. It should not present `Unknown` as a failure.

Windows provides process-specific loopback capture and audio-session process identity, but these capabilities do not replace a virtual endpoint when an application must select a CleanChannel speaker or microphone.

References: [Application loopback sample](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/), [IAudioSessionControl2::GetProcessId](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessioncontrol2-getprocessid)

## Focused-application EQ profiles

Focused profiles are a first-class feature and do not require a separate virtual endpoint for every game.

CleanChannel can observe the foreground window, resolve it to a process, and activate a profile when the process is focused. The Windows `GetForegroundWindow` API provides the foreground window handle; the process identity can then be resolved from that window.

Each focused profile must declare its scope:

- `Master` — affects all processed output.
- `Bus` — affects one logical bus such as `Sound`.
- `Endpoint` — affects one CleanChannel virtual endpoint.
- `Application` — affects only an application path when the application has a dedicated endpoint or isolated capture path.

The default game profile scope is `Bus: Sound`, because it works without automatic per-application routing. It is expected that music or browser audio on the same bus will also receive the temporary EQ. The UI should state this clearly.

Profile changes should use parameter smoothing or a short crossfade so switching EQs does not create an audible click. Profiles need explicit priority, debounce, and restore behavior when focus moves to another application.

Reference: [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow)

## Application endpoint assignment

Windows 10 and later support per-application preferred endpoints through the App volume and device preferences system. This is why products such as Sonar can present a user experience where Spotify, a game, and chat use different virtual channels. [Default endpoint selection](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/default-audio-endpoint-selection)

The important distinction is between:

1. **Endpoint capability** — CleanChannel can expose virtual devices.
2. **User preference** — Windows can remember an application's preferred endpoint.
3. **Automation** — CleanChannel attempts to set that preference for the user.
4. **Verification** — CleanChannel observes the resulting audio session and reports whether it worked.

CleanChannel should implement automation behind an `ApplicationEndpointRouter` adapter. The adapter may require Windows policy interfaces that are less stable or less documented than the normal Core Audio enumeration/session APIs. It must therefore be best-effort, version-tested, and never the only way to use the product.

The supported fallback is:

- Show the expected endpoint name.
- Offer a setup guide or open Windows sound settings.
- Detect the actual session endpoint after the application starts.
- Report `Configured` or `WrongEndpoint`.

Automatic assignment is therefore feasible to prototype, but it should not be treated as a guaranteed Windows contract.

## Browser and website audio

Standalone applications such as Spotify are process-level targets. CleanChannel can bind a profile to the Spotify executable and attempt to assign or verify its endpoint.

YouTube Music running in Chrome, Edge, or another browser is different. Windows normally exposes the browser process/session, not a reliable independent audio endpoint for each website tab. Routing the browser process to `CleanChannel Music` may also route YouTube, Twitch, and other browser audio there.

CleanChannel should initially support:

- Whole-browser routing and verification.
- Focused EQ for the browser process.
- A clear limitation message when the user asks for website-level routing.

Site-level routing can be considered later through an optional browser extension/native integration, but it should not be part of the base Windows audio contract.

## IPC contract direction

The first local IPC contract should be versioned and explicit. Initial operations are expected to include:

- `GetRuntimeState`.
- `GetDevices`.
- `GetChannelSlots`.
- `SetChannelVolume`.
- `SetChannelMute`.
- `SetChannelOutput`.
- `ApplyProfile`.
- `GetApplicationVerification`.
- `SetOperatingMode`.
- `SetFullMute`.
- `SetFullOff`.
- `SubscribeToStateEvents`.

Messages should use input/output separation, machine-readable error codes, and additive evolution. The audio host must reject invalid state at the boundary and must not trust UI strings or external profile files.

## Design rules

- Never run network calls, account checks, or UI callbacks on the audio thread.
- Never dynamically install/remove a virtual endpoint when a game starts.
- Never assume a browser process can be split by website without an explicit browser integration.
- Never make basic local audio depend on a reachable account server.
- Keep endpoint IDs stable.
- Keep the driver thin.
- Treat third-party audio and AI model responses as untrusted external dependencies.
- Log enough information to diagnose routing and device loss without recording microphone audio by default.
