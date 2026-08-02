using CleanChannel.Contracts;

namespace CleanChannel.Core.Runtime;

public sealed class DemoAudioControlService : IAudioControlService
{
    private readonly object _sync = new();
    private AudioControlState _state = CreateInitialState();

    public bool IsDemoBackend => true;

    public Task<AudioControlState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetChannelVolumeAsync(
        string channelId,
        float volume,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateVolume(volume);

        lock (_sync)
        {
            var channel = FindChannel(channelId);
            _state = _state with
            {
                Channels = ReplaceChannel(channel with { Volume = volume }),
            };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetChannelMuteAsync(
        string channelId,
        bool isMuted,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var channel = FindChannel(channelId);
            _state = _state with
            {
                Channels = ReplaceChannel(channel with { IsMuted = isMuted }),
            };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetFullMuteAsync(
        bool isMuted,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _state = _state with { IsFullMute = isMuted };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetFullOffAsync(
        bool isOff,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _state = _state with { IsFullOff = isOff };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetSelectedOutputDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            EnsureOutputDevice(deviceId);
            _state = _state with { SelectedOutputDeviceId = deviceId };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetSelectedInputDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            EnsureInputDevice(deviceId);
            _state = _state with { SelectedInputDeviceId = deviceId };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetChannelOutputDeviceAsync(
        string channelId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            EnsureOutputDevice(deviceId);
            var channel = FindChannel(channelId);
            _state = _state with
            {
                Channels = ReplaceChannel(channel with { SelectedOutputDeviceId = deviceId }),
            };
            return Task.FromResult(Snapshot());
        }
    }

    public Task<AudioControlState> SetChannelEqProfileAsync(
        string channelId,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_state.EqProfiles.Any(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal)))
            {
                throw new ArgumentException($"Unknown EQ profile '{profileId}'.", nameof(profileId));
            }

            var channel = FindChannel(channelId);
            _state = _state with
            {
                Channels = ReplaceChannel(channel with { SelectedEqProfileId = profileId }),
            };
            return Task.FromResult(Snapshot());
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private AudioControlState Snapshot() => _state with { ObservedAt = DateTimeOffset.UtcNow };

    private ChannelState FindChannel(string channelId)
    {
        var channel = _state.Channels.FirstOrDefault(
            candidate => string.Equals(candidate.ChannelId, channelId, StringComparison.Ordinal));

        return channel ?? throw new ArgumentException($"Unknown channel '{channelId}'.", nameof(channelId));
    }

    private ChannelState[] ReplaceChannel(ChannelState replacement)
        => _state.Channels
            .Select(channel => channel.ChannelId == replacement.ChannelId ? replacement : channel)
            .ToArray();

    private void EnsureOutputDevice(string deviceId)
    {
        if (!_state.OutputDevices.Any(device =>
                device.IsAvailable && string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Unknown output device '{deviceId}'.", nameof(deviceId));
        }
    }

    private void EnsureInputDevice(string deviceId)
    {
        if (!_state.InputDevices.Any(device =>
                device.IsAvailable && string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Unknown input device '{deviceId}'.", nameof(deviceId));
        }
    }

    private static void ValidateVolume(float volume)
    {
        if (float.IsNaN(volume) || float.IsInfinity(volume) || volume is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be between 0 and 1.");
        }
    }

    private static AudioControlState CreateInitialState()
    {
        var outputDevices = new[]
        {
            new PhysicalAudioDevice("demo-headset", "Headset (demo device)", AudioDeviceDirection.Render, true, true),
            new PhysicalAudioDevice("demo-speakers", "Speakers (demo device)", AudioDeviceDirection.Render, false, true),
        };
        var inputDevices = new[]
        {
            new PhysicalAudioDevice("demo-microphone", "Microphone (demo device)", AudioDeviceDirection.Capture, true, true),
        };
        var eqProfiles = new[]
        {
            new EqProfile("flat", "Flat", "No EQ curve applied.", IsDemo: true),
            new EqProfile("valorant-focus", "Valorant Focus", "Demo preset for positional detail.", IsDemo: true),
            new EqProfile("voice-clarity", "Voice Clarity", "Demo preset for speech presence.", IsDemo: true),
        };
        var channels = new[]
        {
            new ChannelState("sound", "Sound", 0.80f, false, true, outputDevices[0].DeviceId, eqProfiles[0].ProfileId),
            new ChannelState("chat", "Chat", 0.72f, false, true, outputDevices[0].DeviceId, eqProfiles[2].ProfileId),
            new ChannelState("game", "Game", 0.86f, false, true, outputDevices[0].DeviceId, eqProfiles[1].ProfileId),
            new ChannelState("mic", "Mic", 0.78f, false, true, outputDevices[0].DeviceId, eqProfiles[2].ProfileId),
        };

        return new AudioControlState(
            DateTimeOffset.UtcNow,
            AudioHostReadiness.Degraded,
            "Demo control plane active. No audio is being processed.",
            IsProcessingAvailable: false,
            IsFullMute: false,
            IsFullOff: false,
            SelectedInputDeviceId: inputDevices[0].DeviceId,
            SelectedOutputDeviceId: outputDevices[0].DeviceId,
            inputDevices,
            outputDevices,
            channels,
            eqProfiles);
    }
}
