# CleanChannel Agent Instructions

## Project context

CleanChannel is a Windows-first audio control-plane and audio-host project. The current `0.1` checkpoint is an Avalonia control-plane/licensing demo; it does not yet implement real WASAPI routing, virtual endpoints, production licensing, or signed Pro artifacts.

### Technology

- .NET SDK `10.0.302`, target framework `net10.0`, nullable reference types, implicit usings, latest-recommended analyzers, and warnings treated as errors.
- Avalonia `12.0.4` with `Avalonia.Themes.Fluent` for the desktop UI.
- `CommunityToolkit.Mvvm` `8.4.2` for observable view models and async commands; do not hand-roll an MVVM framework.
- Serilog through `CleanChannel.Diagnostics` for structured local logging.
- Rust stable for the user-mode AudioHost foundation.
- A future full-mode virtual endpoint layer may use C++/WDK; do not imply that it exists in the current demo.

## Commands

Run the repository verification from the root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\verify.ps1 -Configuration Release
```

The script restores, builds, and tests the managed solution and landing site, then runs Rust formatting/tests/build. Use `-IncludePro -ProRoot .\private\CleanChannel.Pro` only when the separate private Pro checkout is present.

For focused work, use the relevant project or manifest rather than broadening the change:

```powershell
dotnet test tests\managed\CleanChannel.App.Tests\CleanChannel.App.Tests.csproj -c Release
dotnet test website\CleanChannel.Landing.Tests\CleanChannel.Landing.Tests.csproj -c Release
cargo test --manifest-path src\native\Cargo.toml
```

## Engineering conventions

- Read `CONTEXT.md`, the relevant ADRs, and the target source/tests before changing behavior. Use the domain terms defined in `CONTEXT.md`.
- Follow existing interfaces and tests before introducing new abstractions. Keep UI concerns in Avalonia view models and keep real-time audio work out of the UI/control plane.
- Prefer async APIs and track/drain background operations during shutdown. Do not block the UI thread on async work.
- Use source-generated or structured Serilog/`Microsoft.Extensions.Logging` messages. Never log microphone samples, audio buffers, credentials, entitlement tokens, or license keys.
- Use only established, actively maintained NuGet packages whose purpose and adoption are clear; verify package versions before adding a dependency.
- Add or update focused tests with behavior changes. Run the full verification script before committing.

## Boundaries

- The public repository may contain `CleanChannel.ProContracts`, but must not contain Pro implementation code, AI models, premium content, signing keys, or production account/billing secrets.
- Basic local audio must remain independent of account availability. Account, billing, entitlement, and cloud work belongs in the control plane, never in the Rust audio callback or driver.
- The current website is a local licensing sandbox. It must fail closed when real production configuration is absent and must not be described as a payment or licensing backend.
- Do not claim real audio routing, virtual devices, NativeAOT publishing, driver signing, or Pro processing until the corresponding implementation and verification exist.
- Preserve user changes and do not reset or discard unrelated work. Keep commits focused by slice when bootstrapping features.

## Context loading

For a feature, load only the relevant context: root `CONTEXT.md`, applicable files in `docs/adr/`, the product/licensing or Pro-boundary document when relevant, target source files, and their tests. Use `README.md` and `docs/roadmap.md` for current capability status, not as a substitute for implementation evidence.

## Agent skills

### Issue tracker

Issues and specs for this repo live as local Markdown files under `.scratch/<feature-slug>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the canonical triage labels `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repository with root-level `CONTEXT.md` and `docs/adr/`. See `docs/agents/domain.md`.
