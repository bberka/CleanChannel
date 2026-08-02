# CleanChannel Context

## Purpose

CleanChannel is a Windows desktop audio utility that gives users explicit control over how application sound and microphone audio are grouped, processed, and delivered to physical devices.

The product is intended to be lightweight, understandable, and safe to recover when an audio device or the CleanChannel process stops working.

## Domain language

### Channel

A user-visible logical audio path such as `Sound`, `Chat`, `Game`, or `Valorant`.

“Channel” does not mean a left/right speaker channel. It means a user-configurable audio destination or source path.

### Virtual endpoint

An audio device that Windows and other applications can select, such as `CleanChannel Chat` or `CleanChannel Mic`.

### Physical device

An actual microphone, headset, speaker, monitor, USB device, or Bluetooth device connected to Windows.

### Render path

Audio moving from an application toward a speaker or headset.

### Capture path

Audio moving from a microphone toward an application.

### Output route

The selected physical device that receives a channel after CleanChannel processing.

### Profile

A named collection of channel routing, volume, EQ, processing, and shortcut settings.

### Profile trigger

A condition that can activate a profile, initially based on a detected application executable and its process tree.

### Audio session

A Windows-managed application audio stream that CleanChannel can observe for process identity, endpoint usage, state, and volume-related information.

### Channel verification

The process of comparing the endpoint used by a detected application audio session with the endpoint expected by its profile.

Verification informs the user; it does not silently change the application's audio setting.

### Active channel

A channel whose endpoint is installed and whose processing/routing path is currently available to the audio host.

### Inactive channel

A channel that remains configured but is not currently being processed or routed. Inactive does not necessarily mean that Windows has removed the endpoint.

### Lite mode

The operating mode that does not depend on CleanChannel virtual endpoints.

### Full mode

The operating mode that provides CleanChannel virtual render and capture endpoints.

## Product principles

- Local audio must continue to work when account services are unavailable.
- User routing choices are more trustworthy than silent application redirection.
- Endpoint identity should remain stable across game launches and profile changes.
- The audio path must fail safely and provide a clear recovery action.
- Advanced processing must not be required for basic volume and routing.
