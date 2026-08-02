# Product and Licensing Model

## Recommendation

CleanChannel should use an open-core model with official signed distributions and separately licensed Pro extensions.

The core should remain useful without an account. Pro entitlement may require an account for billing and recovery, but the audio engine must not depend on a live account service.

This is a better fit for a local Windows utility than making every user register before they can change a volume slider.

## What should remain open

The following should be open and inspectable wherever practical:

- Core UI and tray behavior.
- Basic channel routing and mixing.
- Basic EQ, gain, mute, and volume controls.
- Profile format and profile evaluation rules.
- Endpoint verification logic.
- IPC contracts.
- Driver protocol and driver source, subject to third-party licensing review.
- Lite mode and development tools.

Open core gives the project credibility, makes debugging easier, and allows the community to contribute integrations and profile definitions.

## What should be proprietary Pro functionality

It is reasonable to keep the following as proprietary modules:

- AI noise-cancellation models and model-serving code.
- Premium or commercially licensed DSP algorithms.
- Cloud profile synchronization and backup services.
- Premium preset libraries and curated game profiles.
- Commercial integrations and managed distribution services.
- Paid support tooling or enterprise deployment features.

The closed boundary should be a separate process or native module. Do not make the open core depend on a proprietary assembly that cannot be built by community contributors.

## Preset library strategy

SteelSeries publicly describes a large Sonar preset library and supports sharing presets through Sonar/GG links. That does not establish permission for CleanChannel to scrape, copy, or redistribute the library.

CleanChannel should instead use:

- A documented CleanChannel EQ/profile format.
- User import/export of profiles where the source format is legitimately available.
- Original profiles created by the project.
- Community-contributed profiles with attribution and a clear license.
- Licensed or partnered profile packs if a commercial agreement is obtained.

SteelSeries names may be referenced nominatively when explaining compatibility, but CleanChannel should not present copied SteelSeries presets as its own. See [SteelSeries preset sharing](https://support.steelseries.com/hc/en-us/articles/31606664478477-How-To-Share-Sonar-Presets) and [SteelSeries terms](https://steelseries.com/game-on/terms-and-conditions).

## License tiers

### Community / Lite

- Free to use.
- No account required.
- Core local routing and configuration.
- User-created profiles and EQ.
- Lite mode.
- Full-mode support when the user installs a compatible driver package.

### Pro Annual

- Recurring yearly entitlement.
- AI noise cancellation and other Pro processing.
- Premium presets and profile packs.
- Cloud synchronization if enabled by the user.
- Updates while the subscription is active.

### Pro Lifetime

- One-time purchase.
- Perpetual license to the Pro product.
- No recurring renewal.
- Updates included for the lifetime of the CleanChannel product while it remains available.

The exact legal definition of “lifetime” must be written in the purchase terms. Marketing should not promise unlimited human support or indefinite server operation when the intended promise is unlimited software updates for the product.

## Accounts and offline behavior

- Lite mode must never require an account.
- Pro subscriptions may use an account for billing and entitlement management.
- A lifetime license should be recoverable with a license key or account receipt.
- The app should cache a signed entitlement locally.
- Temporary account-service outages should not immediately stop the audio engine.
- The driver must never perform account validation.

Authentication belongs in the control plane, not in the real-time audio path.

## Signed distribution

Community builds can use test signing on development machines. Official builds should include an officially signed driver package and signed application/installer binaries.

Attestation signing is a useful early distribution path but has restrictions and is not the normal retail Windows Update path. WHQL/Windows Hardware Compatibility Program certification is the long-term production option. [Driver-signing options](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings), [Attestation signing](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-attestation)

The fact that the driver source is open does not prevent official binaries from being signed. Signing identifies the publisher and verifies the package; it does not require the implementation to be closed source.

## Suggested source boundary

```text
Open repository
  - CleanChannel.App
  - CleanChannel.Contracts
  - CleanChannel.Core
  - CleanChannel.AudioHost
  - CleanChannel.Driver source, if licensing and maintenance permit
  - Lite features and tests

Pro distribution
  - CleanChannel.ProHost
  - AI models and model adapters
  - Premium preset packages
  - Cloud synchronization service
  - Official installer and signed release artifacts
```

The repository license is still an explicit decision. A permissive license is the simplest fit for proprietary extensions, but the final choice should account for driver code, DSP dependencies, model licenses, and contributor agreements.

## Business risks to validate

- Users may value a no-account product more than AI features.
- AI processing quality and latency may be difficult to match with established products.
- Driver signing and Windows compatibility will create ongoing release work.
- Lifetime licenses create a long-term support obligation.
- Premium game presets may require continuous maintenance as games and audio engines change.

The first paid feature should be valuable without making the free core feel artificially broken.
