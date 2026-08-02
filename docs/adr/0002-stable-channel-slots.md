# ADR-0002: Use stable channel slots and verify user-selected routing

## Status

Proposed

## Date

2026-08-02

## Context

Users want to create channels for specific games such as Valorant, select those channels inside the game, and have CleanChannel activate the appropriate EQ/profile when the game is detected.

Removing and reinstalling a Windows audio endpoint whenever a game starts risks unstable device identity, lost application settings, and unnecessary administrator/driver operations.

CleanChannel also does not need to force an application to a channel if it can clearly tell the user whether the application is configured correctly.

## Decision

Full mode exposes a finite pool of stable virtual endpoint slots. A user maps a slot to a purpose such as `Valorant`, `CS2`, or `Discord`.

When a profile trigger fires, CleanChannel:

1. Activates the profile's channel graph and EQ.
2. Locates the application's audio session.
3. Compares the session endpoint with the expected channel endpoint.
4. Shows a clear status and repair instruction.

The application does not make automatic endpoint assignment a hard dependency. A best-effort application endpoint router may attempt to set the Windows per-application preference, but every result must be verified and the user must have a manual fallback.

The default inactive behavior is to leave the endpoint installed but stop or bypass its processing route. An optional OS-disabled mode may be considered later, with explicit warnings about application fallback behavior.

## Alternatives considered

### Install/remove a device per game

Rejected because endpoint identity and application settings can become unstable, and device installation requires privileged operations.

### Force per-process routing

Not selected as a hard dependency. It remains an optional adapter because the Windows per-application preference mechanism can be useful, but the automation surface is less stable than normal endpoint/session enumeration.

### Process loopback only

Rejected as the full solution because it does not expose a processed virtual microphone or stable speaker endpoints to applications.

## Consequences

- Users may need to select a channel once inside each application.
- The product must make misconfiguration obvious and easy to fix.
- The driver can be installed once and updated independently.
- Arbitrary channel counts are bounded by the installed slot pool in the first release.
- Future dynamic slot expansion can be added through an explicit installer/update operation rather than a game-launch side effect.
