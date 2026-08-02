# Pro development and private build boundary

CleanChannel keeps the public repository useful without proprietary code. The open repository contains the public `CleanChannel.ProContracts` assembly, but not the implementation of the Pro host, AI models, premium content, account integration, or signing keys.

## Repository layout

```text
Public repository
  src/managed/CleanChannel.ProContracts/       public versioned IPC contracts
  src/managed/CleanChannel.Diagnostics/        local privacy-safe logging
  src/managed/CleanChannel.App/                UI and control plane

Private checkout (ignored by the public repository)
  private/CleanChannel.Pro/CleanChannel.Pro.sln
  private/CleanChannel.Pro/CleanChannel.ProHost/  proprietary implementation
  private/CleanChannel.Pro/artifacts/             private build output
```

The private checkout should reference the public contracts through a project reference in a coordinated local workspace or through a published package. It must not copy Pro implementation into the public solution.

## Builds

Public verification is deliberately Pro-free:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\verify.ps1
```

An explicit private build can include the separate solution:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\verify.ps1 `
  -IncludePro `
  -ProRoot .\private\CleanChannel.Pro
```

The root MSBuild files default `CleanChannelProEnabled` to `false`. Enabling it validates that the private Pro solution exists; it never fetches or synthesizes proprietary code.

## Boundary rules

- `CleanChannel.ProContracts` is additive and versioned for local IPC. Capability identifiers are stable strings so newer Pro hosts can advertise features older clients do not know.
- The Pro handshake carries a client challenge and signed host attestation. `ProHostAttestationValidator` covers the challenge, publisher thumbprint, protocol version, and ECDSA signature checks. The future named-pipe adapter must add a restrictive ACL before the app trusts the response; the current bootstrap does not connect to a Pro host yet.
- The core app can represent `Unavailable` or `Degraded` Pro state and must continue local audio without a Pro host.
- Entitlement DTOs contain state, expiry, and feature identifiers only. License keys, access tokens, account identifiers, and model details stay in the private control-plane implementation.
- Account, billing, cloud synchronization, and entitlement refresh never run in the Rust audio callback or driver.
- Official Pro distribution may contain signed application/host artifacts; signing certificates and installer credentials remain outside the repository.
- The public logging module redacts common credential fields and never intentionally records microphone samples or audio buffers.

The private Pro implementation is responsible for validating entitlements at its own boundary and reporting only the public status contract to the app.
