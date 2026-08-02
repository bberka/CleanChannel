namespace CleanChannel.Core.Runtime;

public interface IAudioControlService : IAsyncDisposable
{
    bool IsDemoBackend { get; }

    Task<AudioControlState> GetStateAsync(CancellationToken cancellationToken = default);

    Task<AudioControlState> SetChannelVolumeAsync(
        string channelId,
        float volume,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetChannelMuteAsync(
        string channelId,
        bool isMuted,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetFullMuteAsync(
        bool isMuted,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetFullOffAsync(
        bool isOff,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetSelectedOutputDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetSelectedInputDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetChannelOutputDeviceAsync(
        string channelId,
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<AudioControlState> SetChannelEqProfileAsync(
        string channelId,
        string profileId,
        CancellationToken cancellationToken = default);
}
