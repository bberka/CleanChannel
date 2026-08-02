using CleanChannel.Contracts;

namespace CleanChannel.Core.Runtime;

public enum AudioHostReadiness
{
    Unavailable,
    Starting,
    Ready,
    Degraded,
    Failed,
}

public sealed record PhysicalAudioDevice(
    string DeviceId,
    string DisplayName,
    AudioDeviceDirection Direction,
    bool IsDefault,
    bool IsAvailable);

public sealed record EqProfile(
    string ProfileId,
    string DisplayName,
    string Description,
    bool IsDemo);

public sealed record ChannelState(
    string ChannelId,
    string DisplayName,
    float Volume,
    bool IsMuted,
    bool IsAvailable,
    string? SelectedOutputDeviceId,
    string? SelectedEqProfileId);

public sealed record AudioControlState(
    DateTimeOffset ObservedAt,
    AudioHostReadiness Readiness,
    string StatusMessage,
    bool IsProcessingAvailable,
    bool IsFullMute,
    bool IsFullOff,
    string? SelectedInputDeviceId,
    string? SelectedOutputDeviceId,
    IReadOnlyList<PhysicalAudioDevice> InputDevices,
    IReadOnlyList<PhysicalAudioDevice> OutputDevices,
    IReadOnlyList<ChannelState> Channels,
    IReadOnlyList<EqProfile> EqProfiles);
