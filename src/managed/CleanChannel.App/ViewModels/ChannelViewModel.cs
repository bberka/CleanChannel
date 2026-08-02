using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CleanChannel.Core.Runtime;

namespace CleanChannel.App.ViewModels;

public sealed class ChannelViewModel : ObservableObject
{
    private readonly Func<ChannelViewModel, Task> _toggleMute;
    private readonly Func<ChannelViewModel, float, Task> _volumeChanged;
    private readonly Func<ChannelViewModel, AudioDeviceViewModel, Task> _outputChanged;
    private readonly Func<ChannelViewModel, EqProfileViewModel, Task> _eqProfileChanged;
    private bool _isApplyingState;
    private float _volume;
    private bool _isMuted;
    private bool _isAvailable;
    private AudioHostReadiness _hostReadiness;
    private bool _isMuteBusy;
    private AudioDeviceViewModel? _selectedOutputDevice;
    private EqProfileViewModel? _selectedEqProfile;
    private Task _pendingChanges = Task.CompletedTask;

    public ChannelViewModel(
        ChannelState state,
        IReadOnlyList<AudioDeviceViewModel> availableOutputDevices,
        IReadOnlyList<EqProfileViewModel> availableEqProfiles,
        Func<ChannelViewModel, Task> toggleMute,
        Func<ChannelViewModel, float, Task> volumeChanged,
        Func<ChannelViewModel, AudioDeviceViewModel, Task> outputChanged,
        Func<ChannelViewModel, EqProfileViewModel, Task> eqProfileChanged)
    {
        ChannelId = state.ChannelId;
        DisplayName = state.DisplayName;
        AvailableOutputDevices = availableOutputDevices;
        AvailableEqProfiles = availableEqProfiles;
        _toggleMute = toggleMute;
        _volumeChanged = volumeChanged;
        _outputChanged = outputChanged;
        _eqProfileChanged = eqProfileChanged;
        ToggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync, CanToggleMute);
        Apply(state, AudioHostReadiness.Starting);
    }

    public string ChannelId { get; }

    public string DisplayName { get; }

    public IReadOnlyList<AudioDeviceViewModel> AvailableOutputDevices { get; }

    public IReadOnlyList<EqProfileViewModel> AvailableEqProfiles { get; }

    public IAsyncRelayCommand ToggleMuteCommand { get; }

    public bool IsMuteBusy
    {
        get => _isMuteBusy;
        private set
        {
            if (!SetProperty(ref _isMuteBusy, value))
            {
                return;
            }

            ToggleMuteCommand.NotifyCanExecuteChanged();
        }
    }

    public Task PendingChanges => _pendingChanges;

    public bool IsAvailable
    {
        get => _isAvailable;
        private set
        {
            if (!SetProperty(ref _isAvailable, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(CanEdit));
            ToggleMuteCommand.NotifyCanExecuteChanged();
        }
    }

    public AudioHostReadiness HostReadiness
    {
        get => _hostReadiness;
        private set
        {
            if (!SetProperty(ref _hostReadiness, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(CanEdit));
            ToggleMuteCommand.NotifyCanExecuteChanged();
        }
    }

    public bool CanEdit => IsAvailable
        && HostReadiness is not (AudioHostReadiness.Unavailable or AudioHostReadiness.Failed);

    public string StatusLabel => !IsAvailable || HostReadiness is (AudioHostReadiness.Unavailable or AudioHostReadiness.Failed)
        ? "Unavailable"
        : HostReadiness.ToString();

    public float Volume
    {
        get => _volume;
        set
        {
            if (!SetProperty(ref _volume, Math.Clamp(value, 0, 1)) || _isApplyingState)
            {
                return;
            }

            OnPropertyChanged(nameof(VolumeLabel));
            TrackChange(() => _volumeChanged(this, _volume));
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (!SetProperty(ref _isMuted, value))
            {
                return;
            }

            OnPropertyChanged(nameof(MuteButtonLabel));
        }
    }

    public AudioDeviceViewModel? SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (!SetProperty(ref _selectedOutputDevice, value) || _isApplyingState || value is null)
            {
                return;
            }

            TrackChange(() => _outputChanged(this, value));
        }
    }

    public EqProfileViewModel? SelectedEqProfile
    {
        get => _selectedEqProfile;
        set
        {
            if (!SetProperty(ref _selectedEqProfile, value) || _isApplyingState || value is null)
            {
                return;
            }

            TrackChange(() => _eqProfileChanged(this, value));
        }
    }

    public string VolumeLabel => $"{Math.Round(Volume * 100):0}%";

    public string AccessibilityName => $"{DisplayName} channel";

    public string VolumeAccessibilityLabel => $"{DisplayName} volume";

    public string MuteAccessibilityLabel => $"Toggle {DisplayName} mute";

    public string OutputAccessibilityLabel => $"{DisplayName} output device";

    public string EqAccessibilityLabel => $"{DisplayName} EQ profile";

    public string MuteButtonLabel => IsMuted ? "Unmute" : "Mute";

    public string RouteLabel => SelectedOutputDevice?.DisplayName ?? "No output selected";

    public void Apply(ChannelState state, AudioHostReadiness hostReadiness)
    {
        _isApplyingState = true;

        try
        {
            Volume = state.Volume;
            IsMuted = state.IsMuted;
            IsAvailable = state.IsAvailable;
            HostReadiness = hostReadiness;
            SelectedOutputDevice = AvailableOutputDevices.FirstOrDefault(
                device => string.Equals(device.DeviceId, state.SelectedOutputDeviceId, StringComparison.Ordinal));
            SelectedEqProfile = AvailableEqProfiles.FirstOrDefault(
                profile => string.Equals(profile.ProfileId, state.SelectedEqProfileId, StringComparison.Ordinal));
        }
        finally
        {
            _isApplyingState = false;
        }

        OnPropertyChanged(nameof(VolumeLabel));
        OnPropertyChanged(nameof(MuteButtonLabel));
        OnPropertyChanged(nameof(RouteLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(CanEdit));
        ToggleMuteCommand.NotifyCanExecuteChanged();
    }

    private bool CanToggleMute() => !IsMuteBusy && CanEdit;

    private async Task ToggleMuteAsync()
    {
        if (IsMuteBusy)
        {
            return;
        }

        IsMuteBusy = true;

        try
        {
            await _toggleMute(this).ConfigureAwait(true);
        }
        finally
        {
            IsMuteBusy = false;
        }
    }

    private void TrackChange(Func<Task> change)
    {
        var changeTask = change();
        _pendingChanges = Task.WhenAll(_pendingChanges, changeTask);
    }
}
