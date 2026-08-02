using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanChannel.Core.Runtime;

namespace CleanChannel.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IAudioControlService _audioService;
    private readonly ControlOperationCoordinator _operationCoordinator = new();
    private readonly object _disposeGate = new();
    private bool _isInitialized;
    private bool _isBusy;
    private AudioHostReadiness _readiness = AudioHostReadiness.Starting;
    private string _statusMessage = "Connecting to AudioHost…";
    private bool _isProcessingAvailable;
    private bool _isFullMute;
    private bool _isFullOff;
    private string? _selectedInputDeviceId;
    private string? _selectedOutputDeviceId;
    private AudioDeviceViewModel? _selectedInputDevice;
    private AudioDeviceViewModel? _selectedOutputDevice;
    private AudioControlState? _lastKnownState;
    private Task _pendingDeviceChanges = Task.CompletedTask;
    private Task? _disposeTask;

    public MainWindowViewModel(IAudioControlService audioService)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => !IsBusy);
        ToggleFullMuteCommand = new AsyncRelayCommand(ToggleFullMuteAsync, () => !IsBusy);
        ToggleFullOffCommand = new AsyncRelayCommand(ToggleFullOffAsync, () => !IsBusy);
    }

    public ObservableCollection<AudioDeviceViewModel> InputDevices { get; } = new();

    public ObservableCollection<AudioDeviceViewModel> OutputDevices { get; } = new();

    public ObservableCollection<EqProfileViewModel> EqProfiles { get; } = new();

    public ObservableCollection<ChannelViewModel> Channels { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ToggleFullMuteCommand { get; }

    public IAsyncRelayCommand ToggleFullOffCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ActionStateLabel));
            RefreshCommand.NotifyCanExecuteChanged();
            ToggleFullMuteCommand.NotifyCanExecuteChanged();
            ToggleFullOffCommand.NotifyCanExecuteChanged();
        }
    }

    public AudioHostReadiness Readiness
    {
        get => _readiness;
        private set
        {
            if (!SetProperty(ref _readiness, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ReadinessLabel));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsProcessingAvailable
    {
        get => _isProcessingAvailable;
        private set
        {
            if (!SetProperty(ref _isProcessingAvailable, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ProcessingLabel));
        }
    }

    public bool IsFullMute
    {
        get => _isFullMute;
        private set
        {
            if (!SetProperty(ref _isFullMute, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FullMuteLabel));
        }
    }

    public bool IsFullOff
    {
        get => _isFullOff;
        private set
        {
            if (!SetProperty(ref _isFullOff, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FullOffLabel));
        }
    }

    public AudioDeviceViewModel? SelectedInputDevice
    {
        get => _selectedInputDevice;
        set
        {
            if (!SetProperty(ref _selectedInputDevice, value) || value is null || value.DeviceId == _selectedInputDeviceId)
            {
                return;
            }

            TrackDeviceChange(() => ChangeSelectedInputDeviceAsync(value));
        }
    }

    public AudioDeviceViewModel? SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (!SetProperty(ref _selectedOutputDevice, value) || value is null || value.DeviceId == _selectedOutputDeviceId)
            {
                return;
            }

            TrackDeviceChange(() => ChangeSelectedOutputDeviceAsync(value));
        }
    }

    public string BackendLabel => _audioService.IsDemoBackend ? "Demo backend" : "AudioHost backend";

    public string ReadinessLabel => Readiness.ToString();

    public string ProcessingLabel => IsProcessingAvailable
        ? "Audio processing is active."
        : "Control plane only — no audio is being processed.";

    public string FullMuteLabel => IsFullMute ? "Unmute all" : "Full mute";

    public string FullOffLabel => IsFullOff ? "Restore audio" : "Full off";

    public string ActionStateLabel => IsBusy ? "Applying…" : "Ready for changes";

    public Task PendingDeviceChanges => _pendingDeviceChanges;

    public void StopAcceptingOperations()
        => _operationCoordinator.StopAccepting();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        if (await RefreshCoreAsync(cancellationToken).ConfigureAwait(true))
        {
            _isInitialized = true;
        }
    }

    public Task RefreshAsync() => RefreshAsync(CancellationToken.None);

    public async Task RefreshAsync(CancellationToken cancellationToken)
        => await RefreshCoreAsync(cancellationToken).ConfigureAwait(true);

    public async Task ToggleFullMuteAsync()
        => await ExecuteOperationAsync(() => _audioService.SetFullMuteAsync(!IsFullMute)).ConfigureAwait(true);

    public async Task ToggleFullOffAsync()
        => await ExecuteOperationAsync(() => _audioService.SetFullOffAsync(!IsFullOff)).ConfigureAwait(true);

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            _operationCoordinator.StopAccepting();
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private Task<bool> RefreshCoreAsync(CancellationToken cancellationToken)
        => ExecuteOperationAsync(
            () => _audioService.GetStateAsync(cancellationToken),
            cancellationToken);

    private async Task ToggleChannelMuteAsync(ChannelViewModel channel)
        => await ExecuteOperationAsync(
            () => _audioService.SetChannelMuteAsync(channel.ChannelId, !channel.IsMuted)).ConfigureAwait(true);

    private async Task ChangeChannelVolumeAsync(ChannelViewModel channel, float volume)
        => await ExecuteOperationAsync(
            () => _audioService.SetChannelVolumeAsync(channel.ChannelId, volume)).ConfigureAwait(true);

    private async Task ChangeChannelOutputAsync(ChannelViewModel channel, AudioDeviceViewModel device)
        => await ExecuteOperationAsync(
            () => _audioService.SetChannelOutputDeviceAsync(channel.ChannelId, device.DeviceId)).ConfigureAwait(true);

    private async Task ChangeChannelEqProfileAsync(ChannelViewModel channel, EqProfileViewModel profile)
        => await ExecuteOperationAsync(
            () => _audioService.SetChannelEqProfileAsync(channel.ChannelId, profile.ProfileId)).ConfigureAwait(true);

    private async Task ChangeSelectedInputDeviceAsync(AudioDeviceViewModel device)
        => await ExecuteOperationAsync(
            () => _audioService.SetSelectedInputDeviceAsync(device.DeviceId)).ConfigureAwait(true);

    private async Task ChangeSelectedOutputDeviceAsync(AudioDeviceViewModel device)
        => await ExecuteOperationAsync(
            () => _audioService.SetSelectedOutputDeviceAsync(device.DeviceId)).ConfigureAwait(true);

    private async Task<bool> ExecuteOperationAsync(
        Func<Task<AudioControlState>> operation,
        CancellationToken cancellationToken = default)
    {
        var state = await _operationCoordinator.RunAsync(
            () => ExecuteOperationCoreAsync(operation, cancellationToken)).ConfigureAwait(true);
        return state is not null;
    }

    private async Task<AudioControlState?> ExecuteOperationCoreAsync(
        Func<Task<AudioControlState>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            IsBusy = true;
            var state = await operation().WaitAsync(cancellationToken).ConfigureAwait(true);
            ApplyState(state);
            return state;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "The requested operation was cancelled.";
            RestoreLastKnownState();
            return null;
        }
        catch (Exception error)
        {
            RestoreLastKnownState();
            Readiness = AudioHostReadiness.Failed;
            IsProcessingAvailable = false;
            StatusMessage = $"AudioHost operation failed: {error.Message}";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _operationCoordinator.DrainAsync().ConfigureAwait(true);

        try
        {
            await _audioService.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _operationCoordinator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ApplyState(AudioControlState state)
    {
        _lastKnownState = state;
        Readiness = state.Readiness;
        StatusMessage = state.StatusMessage;
        IsProcessingAvailable = state.IsProcessingAvailable;
        IsFullMute = state.IsFullMute;
        IsFullOff = state.IsFullOff;
        _selectedInputDeviceId = state.SelectedInputDeviceId;
        _selectedOutputDeviceId = state.SelectedOutputDeviceId;

        ReplaceDevices(InputDevices, state.InputDevices);
        ReplaceDevices(OutputDevices, state.OutputDevices);
        ReplaceProfiles(state.EqProfiles);

        var channelById = Channels.ToDictionary(channel => channel.ChannelId, StringComparer.Ordinal);
        foreach (var channelState in state.Channels)
        {
            if (!channelById.TryGetValue(channelState.ChannelId, out var channel))
            {
                channel = new ChannelViewModel(
                    channelState,
                    OutputDevices,
                    EqProfiles,
                    ToggleChannelMuteAsync,
                    ChangeChannelVolumeAsync,
                    ChangeChannelOutputAsync,
                    ChangeChannelEqProfileAsync);
                channel.Apply(channelState, state.Readiness);
                Channels.Add(channel);
                continue;
            }

            channel.Apply(channelState, state.Readiness);
        }

        SelectedInputDevice = InputDevices.FirstOrDefault(
            device => string.Equals(device.DeviceId, state.SelectedInputDeviceId, StringComparison.Ordinal));
        SelectedOutputDevice = OutputDevices.FirstOrDefault(
            device => string.Equals(device.DeviceId, state.SelectedOutputDeviceId, StringComparison.Ordinal));
    }

    private void RestoreLastKnownState()
    {
        if (_lastKnownState is not null)
        {
            ApplyState(_lastKnownState);
        }
    }

    private void TrackDeviceChange(Func<Task> change)
    {
        var changeTask = change();
        _pendingDeviceChanges = Task.WhenAll(_pendingDeviceChanges, changeTask);
    }

    private static void ReplaceDevices(
        ObservableCollection<AudioDeviceViewModel> target,
        IReadOnlyList<PhysicalAudioDevice> devices)
    {
        target.Clear();
        foreach (var device in devices.Where(device => device.IsAvailable))
        {
            target.Add(new AudioDeviceViewModel(device));
        }
    }

    private void ReplaceProfiles(IReadOnlyList<EqProfile> profiles)
    {
        EqProfiles.Clear();
        foreach (var profile in profiles)
        {
            EqProfiles.Add(new EqProfileViewModel(profile));
        }
    }
}
