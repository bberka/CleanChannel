using CleanChannel.Contracts;
using CleanChannel.Core.Runtime;

namespace CleanChannel.App.ViewModels;

public sealed class AudioDeviceViewModel
{
    public AudioDeviceViewModel(PhysicalAudioDevice device)
    {
        DeviceId = device.DeviceId;
        DisplayName = device.DisplayName;
        Direction = device.Direction;
        IsDefault = device.IsDefault;
        IsAvailable = device.IsAvailable;
    }

    public string DeviceId { get; }

    public string DisplayName { get; }

    public AudioDeviceDirection Direction { get; }

    public bool IsDefault { get; }

    public bool IsAvailable { get; }

    public string DefaultLabel => IsDefault ? "Default" : string.Empty;

    public override string ToString() => DisplayName;
}
