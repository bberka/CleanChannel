# ADR-0004: Use focused profiles with best-effort application routing

## Status

Proposed

## Date

2026-08-02

## Context

The simplest useful game automation is to detect the game that currently owns the foreground window and swap an EQ profile on the selected CleanChannel bus. This works even when all audio is using one `Sound` channel, although the EQ also affects other audio on that bus.

Sonar-like products can also provide separate application-to-channel behavior. Windows supports per-application preferred endpoints, but the public Core Audio APIs and the automation mechanism used by a third-party product are not the same thing. CleanChannel must distinguish what it can reliably verify from what it can only attempt to configure.

Browser audio creates an additional boundary: Windows generally identifies Chrome or Edge as the process, not YouTube Music as a separate application from other tabs.

## Decision

Implement the following in order:

1. Foreground-window profile triggers.
2. Profile scopes for master, bus, endpoint, and isolated application paths.
3. Session-to-endpoint verification.
4. An optional best-effort application endpoint router.
5. Browser-wide routing before any website-level integration.

The default Valorant behavior is:

```text
Valorant focused
  -> activate Valorant EQ on Sound bus
  -> smooth the transition
  -> restore the previous profile when focus leaves Valorant
```

If the user assigns Valorant to a dedicated virtual endpoint, the same profile can use endpoint scope and affect only that channel.

The application endpoint router must be isolated behind an adapter and must always report whether its attempt succeeded. Failure falls back to instructions and verification rather than silently changing defaults or pretending the application was routed.

## Alternatives considered

### Separate endpoint required for every game

Rejected as the baseline because focused bus EQ solves the common case with fewer devices and less setup.

### Process loopback as the only application isolation mechanism

Rejected as the primary product path because it adds capture/mute complexity and does not solve virtual microphone delivery.

### Browser tab routing in the base app

Rejected because the Windows audio session boundary generally stops at the browser process. Website-level identification requires a separate browser integration and should be treated as a later experiment.

## Consequences

- Focused EQ profiles can ship before automatic app routing is complete.
- Users can choose between simple bus-level behavior and precise endpoint-level behavior.
- Music and other audio on the same bus may receive a game EQ; the UI must disclose the scope.
- Spotify can be targeted as a standalone process.
- YouTube Music can initially be routed only as part of the browser process.
- A failed automation attempt remains recoverable and understandable.
