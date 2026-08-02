# CleanChannel

CleanChannel is a Windows-first audio routing and enhancement application inspired by the useful parts of Sonar and Voicemeeter, with a simpler control surface and stronger automation.

The goal is to provide stable virtual audio channels for games, chat applications, and general system sound; per-channel output routing; custom EQ and gain; microphone processing; tray controls; global shortcuts; and automatic profile verification.

> Status: pre-alpha bootstrap. The repository now contains the managed tray shell, public contracts, logging foundation, profile core, and Rust AudioHost lifecycle placeholder. Real WASAPI routing and the virtual driver are still future slices.

## Product direction

CleanChannel is designed for people who want:

- A lightweight tray application instead of a large audio suite.
- Independent `Sound`, `Chat`, `Game`, and user-created channels.
- Per-channel output device selection and volume control.
- User-controlled game routing with automatic verification.
- Automatic EQ/profile activation when a game is detected.
- Focused-application EQ profiles that can work even when several applications share one channel.
- A processed virtual microphone, mic preview, sidetone, and noise reduction.
- Full mute and full-off recovery controls.
- No account requirement for basic local audio functionality.

CleanChannel will not silently force a game or chat application to a channel by default. The user can select the endpoint in the application, CleanChannel can make a best-effort attempt to configure Windows' per-application preference, and the app reports whether the detected audio session is using the expected endpoint.

## Planned operating modes

### Lite mode

Lite mode uses physical Windows audio endpoints and user-mode processing. It is useful for validating the UI, mixer, EQ, microphone chain, profiles, and device switching without installing a CleanChannel virtual driver.

Lite mode cannot expose arbitrary CleanChannel speaker and microphone endpoints to other applications.

### Full mode

Full mode adds stable virtual render and capture endpoints. The driver only transports audio between Windows and the user-mode audio host. Mixing, EQ, gain, noise cancellation, profiles, and licensing remain outside the driver.

## Architecture at a glance

```text
Windows apps
    │
    ├── CleanChannel virtual render endpoints
    │       └── Sound / Chat / Game / user channel slots
    │
    └── CleanChannel virtual microphone endpoint

CleanChannel UI and tray
    └── versioned named-pipe IPC
            └── AudioHost
                    ├── WASAPI/MMDevice integration
                    ├── routing and mixing
                    ├── EQ, gain, meters, mute, sidetone
                    ├── microphone processing
                    └── physical output and microphone devices
```

The planned implementation uses C#/.NET 10 and Avalonia for the UI/control plane, Rust for the real-time user-mode audio host, and C++/WDK for the virtual audio driver. See [docs/architecture.md](docs/architecture.md) and the ADRs in [docs/adr](docs/adr).

## Documentation

- [Project context and domain language](CONTEXT.md)
- [System architecture](docs/architecture.md)
- [Product and licensing model](docs/product-and-licensing.md)
- [Roadmap](docs/roadmap.md)
- [ADR-0001: Hybrid Windows audio stack](docs/adr/0001-hybrid-windows-audio-stack.md)
- [ADR-0002: Stable channel slots and verification](docs/adr/0002-stable-channel-slots.md)
- [ADR-0003: Open core and official Pro distribution](docs/adr/0003-open-core-and-pro-distribution.md)
- [ADR-0004: Focused profiles and best-effort app routing](docs/adr/0004-focused-profiles-and-best-effort-app-routing.md)
- [Pro development and private build boundary](docs/pro-development.md)
- [ADR-0005: Privacy-safe logging and an optional Pro host](docs/adr/0005-observability-and-pro-boundary.md)

## First milestone

The first vertical slice should prove one complete audio path:

1. Capture one CleanChannel render endpoint.
2. Render it to the selected physical output.
3. Provide volume, mute, gain, and one basic EQ chain.
4. Add one processed microphone path and mic preview.
5. Expose tray controls and a minimal settings window.
6. Survive physical device removal and reconnection.

Profiles, game detection, AI noise cancellation, premium presets, and cloud features come after this path is reliable.

## Development prerequisites

The current bootstrap can be built and tested with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\verify.ps1
```

The planned Windows development environment for the full product is:

- Windows 10 or Windows 11, x64 first.
- .NET 10 SDK.
- Visual Studio with desktop C++ tooling.
- Windows Driver Kit for the virtual endpoint work.
- Rust stable toolchain for the audio host.

The public build does not contain closed-source Pro implementation code. See [Pro development and private build boundary](docs/pro-development.md) for the opt-in private build.

Driver development should begin on a dedicated test machine. Official user builds will require a Microsoft-signed driver package.

## Contributing

The intended project model is open core: the basic routing and audio infrastructure should be inspectable and contributable, while separately distributed Pro extensions may remain proprietary. See [docs/product-and-licensing.md](docs/product-and-licensing.md).

Before implementation begins, architectural changes should be captured in an ADR and the domain language in [CONTEXT.md](CONTEXT.md) should remain consistent.
