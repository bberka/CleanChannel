namespace CleanChannel.Core.Channels;

public sealed record ChannelSlot(
    int SlotIndex,
    string DisplayName,
    string? EndpointId = null,
    bool IsEnabled = true,
    bool IsActive = false,
    string? SelectedOutputDeviceId = null);
