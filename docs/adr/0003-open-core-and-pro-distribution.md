# ADR-0003: Use open core with official signed distribution and proprietary Pro extensions

## Status

Proposed

## Date

2026-08-02

## Context

CleanChannel is needed by its solo developer and may become a product. A fully open project with community self-signed drivers is accessible to developers but produces a poor experience for ordinary Windows users. A fully closed application with a mandatory account would make a local audio utility less trustworthy and add operational cost.

The product also has potential paid value in AI noise cancellation, premium presets, cloud synchronization, and commercial support. The intended commercial plans are an annual subscription and a lifetime license with updates included.

## Decision

Use an open-core model:

- Keep core routing, basic DSP, profiles, verification, contracts, and Lite mode open.
- Keep Pro processing, AI models, premium content, and cloud services separately distributed and proprietary where their maintenance or third-party licensing requires it.
- Distribute official application and driver binaries through an installer signed by the project publisher.
- Do not require an account for Lite mode or basic local audio.
- Allow Pro entitlement checks in the control plane, with locally cached signed entitlements so temporary account-service outages do not stop audio.
- Never put licensing or network calls in the driver or real-time audio callback.

The exact open-source license is intentionally a follow-up decision. A permissive license is a candidate because proprietary Pro processes must be able to consume stable public contracts, but the choice must account for driver code, DSP dependencies, model licenses, and contributor terms.

## Alternatives considered

### Fully open source with self-signed drivers for everyone

Rejected as the default end-user distribution. Test signing is appropriate for development, not a normal consumer installation experience.

### Fully closed source with mandatory accounts

Rejected because it creates unnecessary friction for basic local audio, makes the product dependent on account infrastructure, and weakens community trust.

### Open core with official signed binaries

Selected because it preserves inspectability and community contribution while giving ordinary users a supported installation path.

## Consequences

- The project maintains two meaningful boundaries: open core versus Pro extensions, and development driver packages versus official signed packages.
- Pro features must fail gracefully when unavailable.
- The project needs license terms, payment/entitlement handling, privacy documentation, and a driver release process before public distribution.
- A lifetime license creates a long-term update obligation and must be defined precisely in purchase terms.
- The core remains valuable even if a user never buys Pro.
