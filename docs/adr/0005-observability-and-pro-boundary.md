# ADR-0005: Use privacy-safe local logging and a separate Pro host boundary

## Status

Accepted

## Date

2026-08-02

## Context

CleanChannel needs useful diagnostics for device loss, routing failures, process lifecycle, and future IPC issues. It must not turn a local audio utility into a telemetry product or put sensitive audio/account material into log files.

The commercial plan also includes closed-source Pro functionality such as AI noise cancellation, premium DSP, premium presets, synchronization, and entitlement handling. The public build must remain useful and buildable without those modules, while a private distribution must be able to add them without making the real-time path depend on an account service.

## Decision

- Use a small open `CleanChannel.Diagnostics` module backed by Serilog through the `Microsoft.Extensions.Logging` bridge.
- Write structured local logs under `%LOCALAPPDATA%\CleanChannel\Logs` by default.
- Use UTC timestamps, severity/category/event identifiers, daily and size rollover, bounded retention, and opt-in console output.
- Treat the configured file size as the Serilog rollover threshold: the sink rolls before a subsequent event, so one final event can make a file slightly larger than the threshold while retention remains bounded.
- Redact common credential fields, bearer values, license material, and audio sample fields before writing. Logging is diagnostic only and must never be called from an audio callback.
- Keep proprietary implementation in a separate private `CleanChannel.Pro` checkout and process.
- Keep only versioned, source-generated `CleanChannel.ProContracts` in the public repository.
- Authenticate the Pro local IPC handshake with a per-connection challenge and signed host attestation; the public contracts include the validator for this cryptographic check. The future IPC adapter must enforce a restrictive local ACL before accepting entitlement state.
- Default all public builds to `CleanChannelProEnabled=false`; private builds opt in explicitly and validate their private solution path.
- Represent Pro absence or degradation as normal status, never as a reason to stop core local audio.

## Alternatives considered

### Log microphone/audio data for easier support

Rejected. Audio samples are sensitive, logs grow quickly, and they are not needed to diagnose endpoint identity, lifecycle, or routing state. Reproduction captures should be explicit, user-controlled product features with separate consent and storage rules.

### Load a closed Pro assembly into the NativeAOT app

Rejected. Dynamic loading complicates trimming, signing, deployment, and failure recovery. A separate process gives the public app a stable seam and keeps Pro crashes out of the basic audio/control path.

### Require a live account for every feature

Rejected. Basic local audio must survive account-service outages. Entitlement state may be cached and reported through the control plane, but the driver and audio callback remain account-free.

## Consequences

- Developers can diagnose normal failures from local files without uploading user audio.
- Log files need retention and redaction tests as the format evolves.
- Pro development requires a separate private checkout and release pipeline.
- The public IPC contract becomes a compatibility surface and must evolve additively.
- Pro host identity, challenge validation, and local IPC authorization become release-gating security checks for official builds.
- Pro functionality must expose clear unavailable/degraded states in the UI rather than silently changing core behavior.
