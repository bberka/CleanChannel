using System.Text.Json.Serialization;

namespace CleanChannel.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<OperatingMode>))]
public enum OperatingMode
{
    Lite,
    Full,
}

[JsonConverter(typeof(JsonStringEnumConverter<AudioDeviceDirection>))]
public enum AudioDeviceDirection
{
    Render,
    Capture,
}

[JsonConverter(typeof(JsonStringEnumConverter<ProfileVerificationStatus>))]
public enum ProfileVerificationStatus
{
    NotRunning,
    WaitingForAudio,
    Configured,
    WrongEndpoint,
    EndpointUnavailable,
    Unknown,
}

public sealed record AudioDeviceDto(
    string DeviceId,
    string DisplayName,
    AudioDeviceDirection Direction,
    bool IsDefault,
    bool IsAvailable);

public sealed record ChannelSlotDto(
    int SlotIndex,
    string DisplayName,
    string? EndpointId,
    bool IsEnabled,
    bool IsActive,
    string? SelectedOutputDeviceId,
    float Volume,
    bool IsMuted);

public sealed record ProfileVerificationDto(
    string ProfileId,
    string ProfileName,
    ProfileVerificationStatus Status,
    string? ProcessName,
    string? ExpectedEndpointId,
    string? ActualEndpointId,
    string? Message);

public sealed record RuntimeStateDto(
    DateTimeOffset ObservedAt,
    OperatingMode Mode,
    bool IsFullMute,
    bool IsFullOff,
    bool AudioHostConnected,
    IReadOnlyList<AudioDeviceDto> Devices,
    IReadOnlyList<ChannelSlotDto> ChannelSlots,
    IReadOnlyList<ProfileVerificationDto> ProfileVerifications);

public sealed record GetRuntimeStateRequest;

public sealed record GetRuntimeStateResponse(RuntimeStateDto State);

public sealed record GetDevicesRequest;

public sealed record GetDevicesResponse(IReadOnlyList<AudioDeviceDto> Devices);

public sealed record GetChannelSlotsRequest;

public sealed record GetChannelSlotsResponse(IReadOnlyList<ChannelSlotDto> ChannelSlots);

public sealed record GetApplicationVerificationRequest(string ProfileId);

public sealed record GetApplicationVerificationResponse(ProfileVerificationDto Verification);

public sealed record IpcErrorDto(string Code, string Message);

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AudioDeviceDto))]
[JsonSerializable(typeof(OperatingMode))]
[JsonSerializable(typeof(AudioDeviceDirection))]
[JsonSerializable(typeof(ProfileVerificationStatus))]
[JsonSerializable(typeof(ChannelSlotDto))]
[JsonSerializable(typeof(ProfileVerificationDto))]
[JsonSerializable(typeof(RuntimeStateDto))]
[JsonSerializable(typeof(GetRuntimeStateRequest))]
[JsonSerializable(typeof(GetRuntimeStateResponse))]
[JsonSerializable(typeof(GetDevicesRequest))]
[JsonSerializable(typeof(GetDevicesResponse))]
[JsonSerializable(typeof(GetChannelSlotsRequest))]
[JsonSerializable(typeof(GetChannelSlotsResponse))]
[JsonSerializable(typeof(GetApplicationVerificationRequest))]
[JsonSerializable(typeof(GetApplicationVerificationResponse))]
[JsonSerializable(typeof(IpcErrorDto))]
public partial class CleanChannelJsonContext : JsonSerializerContext;
