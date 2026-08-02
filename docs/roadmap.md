# CleanChannel Roadmap

This roadmap is ordered by technical risk and user value. Each phase should leave a working build.

## Current 0.1 demo checkpoint

- [x] Windows Fluent-themed Avalonia tray and control-plane demo using the official `Avalonia.Themes.Fluent` package and `CommunityToolkit.Mvvm`.
- [x] Built-in Avalonia `SplitView` desktop shell with a left sidebar for Dashboard, Channels, Devices, Profiles, and Settings.
- [x] Local demo controls for volume, mute, full mute, full off/restore, device/EQ selection, shortcuts, status, and themes.
- [x] Local landing-site licensing sandbox with registration, demo annual/lifetime flows, activation, and protected placeholder Pro download.
- [x] Fail-fast verification for the managed solution, landing site, and Rust checks.

The sidebar is the desktop shell/navigation layer and remains separate from the real-time audio plane. The checkpoint is intentionally a control-plane and licensing demo. It does not include real WASAPI processing, virtual render/capture devices, a production payment or licensing backend, or signed Pro artifacts.

## Phase 0: Design and feasibility

- [x] Define domain language and product principles.
- [x] Choose hybrid UI, audio host, and driver boundaries.
- [x] Choose stable endpoint slots instead of dynamic per-game device installation.
- [x] Create the initial solution and build pipeline.
- [x] Select Apache License 2.0 for the public core and document the Pro boundary.
- [ ] Confirm the first virtual endpoint development strategy.

Checkpoint: the repository builds a minimal UI and the audio/driver spike has a written result.

## Phase 1: Lite mode

- [ ] Tray application and single-instance behavior.
- [ ] Physical input/output enumeration.
- [ ] Basic render mixer.
- [ ] Volume, mute, gain, and basic EQ.
- [ ] Microphone capture chain and mic preview.
- [ ] Physical device disconnect/reconnect handling.
- [ ] Full mute and full-off recovery behavior.
- [ ] Versioned local IPC.

Checkpoint: a user can process physical audio and microphone input without a virtual driver.

## Phase 2: Full-mode endpoint spike

- [ ] One virtual render endpoint.
- [ ] One virtual microphone endpoint.
- [ ] AudioHost transport to and from the endpoints.
- [ ] Device installation and uninstall recovery.
- [ ] Test-signed development package.
- [ ] Latency, glitch, and crash tests.

Checkpoint: a game and chat application can select the virtual endpoints and audio reaches the intended physical device.

## Phase 3: Channels and verification

- [ ] Stable channel slot pool.
- [ ] User-defined channel names and mappings.
- [ ] Per-channel output selection.
- [ ] Per-channel volume and EQ.
- [ ] Process/profile trigger detection.
- [ ] Foreground-window profile activation.
- [ ] Profile scope selection: master, bus, endpoint, or application.
- [ ] Session-to-endpoint verification.
- [ ] Clear `WrongEndpoint`, `WaitingForAudio`, and `EndpointUnavailable` states.
- [ ] Discord and one game as initial verification targets.

Checkpoint: users select endpoints themselves and CleanChannel reliably explains whether the setup is correct.

## Phase 4: Product baseline

- [ ] Global shortcuts.
- [ ] Best-effort application endpoint assignment adapter.
- [ ] Windows slider synchronization modes.
- [ ] Automatic output selection for newly connected devices.
- [ ] Profile restore policies.
- [ ] Side tone and safe feedback controls.
- [ ] Preset import/export.
- [ ] Diagnostics and support bundle generation.

Checkpoint: the product is comfortable for daily personal use.

## Phase 5: Pro features

- [ ] Pro extension process and entitlement boundary.
- [ ] AI noise cancellation evaluation.
- [ ] Annual subscription entitlement.
- [ ] Lifetime license entitlement.
- [ ] Offline entitlement cache and recovery.
- [ ] Premium preset delivery.
- [ ] Optional cloud profile synchronization.
- [ ] Optional browser integration research for website-level audio identification.

Checkpoint: Pro features can be unavailable without breaking core local audio.

## Phase 6: Public release

- [ ] Driver signing strategy selected: attestation or HLK/WHCP.
- [ ] Signed installer and application binaries.
- [ ] Upgrade, rollback, and uninstall testing.
- [ ] Windows 10/11 support matrix.
- [ ] Crash reporting and privacy documentation.
- [ ] Public documentation and troubleshooting guide.
- [ ] License terms and refund policy.
- [ ] Audit third-party license notices and attribution for release packaging.

## Explicitly not doing initially

- Silent forced routing of games or chat applications.
- Dynamic driver installation/removal on every game launch.
- Assuming YouTube Music can be distinguished from other browser tabs without a browser integration.
- Cloud-first configuration.
- AI before the deterministic audio path is stable.
- A large preset catalog before profile import/export works.
- A Windows service for the real-time audio loop.
