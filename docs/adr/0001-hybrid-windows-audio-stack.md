# ADR-0001: Use a hybrid Windows audio stack

## Status

Proposed

## Date

2026-08-02

## Context

CleanChannel needs a polished tray UI, Windows-visible virtual audio endpoints, low-latency routing, microphone processing, custom DSP, and future AI effects.

The developer is most productive in C#, but the real-time audio path and virtual endpoint layer have different constraints from the UI. The current repository does not yet contain an implementation, so the boundary can be chosen before code is written.

## Decision

Use three primary layers:

1. C#/.NET 10 with Avalonia for the UI and control plane.
2. A separate Rust user-mode AudioHost for WASAPI, routing, mixing, and real-time DSP.
3. A C++/WDK virtual audio driver for Full mode.

The UI and AudioHost communicate through a versioned local named-pipe contract. The driver is a transport boundary and does not contain account checks, cloud calls, profile policy, or complex product DSP.

Lite mode will operate without CleanChannel virtual endpoints. Full mode will use stable virtual render and capture endpoints.

## Alternatives considered

### All C#

Pros:

- Lowest language-switching cost.
- Fast UI and product feature development.

Cons:

- Real-time audio code would share a process and runtime concerns with product/UI code unless carefully isolated.
- NativeAOT and dynamic audio/plugin dependencies require extra care.

Rejected as the primary long-term boundary, but a C# prototype may be useful for UI-only or non-real-time experiments.

### All Rust

Pros:

- One native language for host and tooling.
- Strong fit for real-time ownership and concurrency.

Cons:

- Slower product/UI iteration for the primary developer.
- Less convenient for the planned Windows settings and tray surface.

Rejected because it spends the developer's strongest productivity advantage on the UI layer.

### Driverless only

Pros:

- No virtual driver installation or signing work.
- Simple distribution.

Cons:

- Cannot provide stable application-visible CleanChannel speaker and microphone endpoints.
- Cannot reliably provide the full Sonar-like channel model.

Rejected as the only product mode; retained as Lite mode.

## Consequences

- The project has a clear separation between control-plane failures and audio-plane failures.
- The UI can remain C# while the real-time host remains native and independently testable.
- Driver signing becomes a release workstream but does not block Lite-mode development.
- Pro modules can be isolated without adding license or network work to the audio callback.
- IPC contracts must be versioned and tested from the beginning.
