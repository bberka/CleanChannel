using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using CleanChannel.App;
using CleanChannel.App.ViewModels;
using CleanChannel.Core.Runtime;
using Xunit;

namespace CleanChannel.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ApplicationInitializeLoadsFluentStylesAndTrayDefinitions()
    {
        var app = new App();

        app.Initialize();

        try
        {
            Assert.NotEmpty(app.Styles);
            var trayIcons = TrayIcon.GetIcons(app);
            Assert.NotNull(trayIcons);
            Assert.Single(trayIcons!);
        }
        finally
        {
            app.Dispose();
        }
    }

    [Fact]
    public async Task InitializeRetriesAfterRefreshFailureInsteadOfLatchingFailure()
    {
        await using var service = new TestAudioControlService { RefreshFailuresRemaining = 1 };
        await using var viewModel = new MainWindowViewModel(service);

        await viewModel.InitializeAsync();

        Assert.Empty(viewModel.Channels);
        Assert.Equal(AudioHostReadiness.Failed, viewModel.Readiness);

        await viewModel.InitializeAsync();

        Assert.Equal(4, viewModel.Channels.Count);
        Assert.Equal(AudioHostReadiness.Degraded, viewModel.Readiness);
        Assert.Equal(2, service.RefreshCalls);
    }

    [Fact]
    public async Task InitializesDevicesChannelsAndSurfacesDegradedHostStatus()
    {
        await using var viewModel = new MainWindowViewModel(new DemoAudioControlService());

        await viewModel.InitializeAsync();

        var game = Assert.Single(viewModel.Channels, channel => channel.ChannelId == "game");
        Assert.Equal(AudioHostReadiness.Degraded, viewModel.Readiness);
        Assert.False(viewModel.IsProcessingAvailable);
        Assert.Equal(4, viewModel.Channels.Count);
        Assert.Equal("Headset (demo device)", viewModel.SelectedOutputDevice?.DisplayName);
        Assert.Equal("Degraded", game.StatusLabel);
    }

    [Fact]
    public async Task NavigationSelectionUpdatesCurrentPageAndVisibility()
    {
        await using var viewModel = new MainWindowViewModel(new DemoAudioControlService());

        await viewModel.InitializeAsync();

        Assert.Equal(MainWindowViewModel.DashboardPageKey, viewModel.CurrentPageKey);
        Assert.True(viewModel.IsDashboardPageVisible);

        viewModel.SelectedNavigationItem = MainWindowViewModel.DevicesPageKey;

        Assert.Equal(MainWindowViewModel.DevicesPageKey, viewModel.CurrentPageKey);
        Assert.True(viewModel.IsDevicesPageVisible);
        Assert.False(viewModel.IsDashboardPageVisible);

        viewModel.SelectedNavigationItem = MainWindowViewModel.ChannelsPageKey;

        Assert.Equal(MainWindowViewModel.ChannelsPageKey, viewModel.CurrentPageKey);
        Assert.True(viewModel.IsChannelsPageVisible);
    }

    [Fact]
    public void ChannelStatusSurfacesUnavailableStateAndDisablesEditing()
    {
        var channel = new ChannelViewModel(
            new ChannelState("chat", "Chat", 0.5f, false, false, null, null),
            Array.Empty<AudioDeviceViewModel>(),
            Array.Empty<EqProfileViewModel>(),
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        Assert.Equal("Unavailable", channel.StatusLabel);
        Assert.False(channel.CanEdit);

        channel.Apply(
            new ChannelState("chat", "Chat", 0.5f, false, true, null, null),
            AudioHostReadiness.Degraded);

        Assert.Equal("Degraded", channel.StatusLabel);
        Assert.True(channel.CanEdit);
    }

    [Fact]
    public async Task FullMuteAndFullOffToggleBothWaysAndPersistInBackend()
    {
        await using var service = new DemoAudioControlService();
        await using var viewModel = new MainWindowViewModel(service);
        await viewModel.InitializeAsync();

        await viewModel.ToggleFullMuteAsync();
        await viewModel.ToggleFullOffAsync();
        await viewModel.ToggleFullMuteAsync();
        await viewModel.ToggleFullOffAsync();

        var state = await service.GetStateAsync();
        Assert.False(viewModel.IsFullMute);
        Assert.False(viewModel.IsFullOff);
        Assert.False(state.IsFullMute);
        Assert.False(state.IsFullOff);
        Assert.Equal("Full mute", viewModel.FullMuteLabel);
        Assert.Equal("Full off", viewModel.FullOffLabel);
    }

    [Fact]
    public async Task ChannelVolumeDeviceAndEqChangesAreAwaitedAndPersistInBackend()
    {
        await using var service = new DemoAudioControlService();
        await using var viewModel = new MainWindowViewModel(service);
        await viewModel.InitializeAsync();
        var game = Assert.Single(viewModel.Channels, channel => channel.ChannelId == "game");
        var speakers = Assert.Single(viewModel.OutputDevices, device => device.DeviceId == "demo-speakers");
        var valorant = Assert.Single(viewModel.EqProfiles, profile => profile.ProfileId == "valorant-focus");

        game.Volume = 0.33f;
        game.SelectedOutputDevice = speakers;
        game.SelectedEqProfile = valorant;
        await game.PendingChanges;

        var state = await service.GetStateAsync();
        var gameState = Assert.Single(state.Channels, channel => channel.ChannelId == "game");
        Assert.Equal(0.33f, gameState.Volume);
        Assert.Equal("demo-speakers", gameState.SelectedOutputDeviceId);
        Assert.Equal("valorant-focus", gameState.SelectedEqProfileId);
    }

    [Fact]
    public async Task PhysicalDeviceSelectionIsAwaitedAndPersistsInBackend()
    {
        await using var service = new DemoAudioControlService();
        await using var viewModel = new MainWindowViewModel(service);
        await viewModel.InitializeAsync();
        var speakers = Assert.Single(viewModel.OutputDevices, device => device.DeviceId == "demo-speakers");

        viewModel.SelectedOutputDevice = speakers;
        await viewModel.PendingDeviceChanges;

        var state = await service.GetStateAsync();
        Assert.Equal("demo-speakers", state.SelectedOutputDeviceId);
        Assert.Equal("Speakers (demo device)", viewModel.SelectedOutputDevice?.DisplayName);
    }

    [Fact]
    public async Task FailedChannelChangeIsAwaitedAndRevertsToLastKnownState()
    {
        await using var service = new TestAudioControlService { FailMutations = true };
        await using var viewModel = new MainWindowViewModel(service);
        service.FailMutations = false;
        await viewModel.InitializeAsync();
        service.FailMutations = true;
        var game = Assert.Single(viewModel.Channels, channel => channel.ChannelId == "game");
        var originalVolume = game.Volume;

        game.Volume = 0.12f;
        await game.PendingChanges;

        Assert.Equal(originalVolume, game.Volume);
        Assert.Equal(AudioHostReadiness.Failed, viewModel.Readiness);
        Assert.Contains("failed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BusyStateNotifiesCommandsAndDoesNotDropOperation()
    {
        await using var service = new TestAudioControlService { BlockFullMute = true };
        await using var viewModel = new MainWindowViewModel(service);
        await viewModel.InitializeAsync();
        var canExecuteChanged = 0;
        viewModel.ToggleFullMuteCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        var operation = viewModel.ToggleFullMuteAsync();
        await service.FullMuteStarted.Task;

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.ToggleFullMuteCommand.CanExecute(null));
        Assert.True(canExecuteChanged > 0);

        service.ReleaseFullMute.TrySetResult(true);
        await operation;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.ToggleFullMuteCommand.CanExecute(null));
        Assert.True(viewModel.IsFullMute);
    }

    [Fact]
    public async Task MainWindowRegistersFullMuteAndFullOffShortcutsWithoutForcingTheme()
    {
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .SetupWithoutStarting();
        var service = new DemoAudioControlService();
        var window = new MainWindow(service);

        try
        {
            var gestures = window.KeyBindings.Select(binding => Assert.IsType<KeyGesture>(binding.Gesture)).ToArray();
            Assert.Contains(gestures, gesture => gesture.Key == Key.M && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift));
            Assert.Contains(gestures, gesture => gesture.Key == Key.O && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift));
            Assert.Null(window.RequestedThemeVariant);

            await window.ViewModel.InitializeAsync();

            var muteBinding = Assert.Single(
                window.KeyBindings,
                binding => binding.Gesture is KeyGesture gesture
                    && gesture.Key == Key.M
                    && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift));
            Assert.NotNull(muteBinding.Command);
            muteBinding.Command!.Execute(null);
            Assert.NotNull(window.ViewModel.ToggleFullMuteCommand.ExecutionTask);
            await window.ViewModel.ToggleFullMuteCommand.ExecutionTask!;

            var muteState = await service.GetStateAsync();
            Assert.True(muteState.IsFullMute);

            var fullOffBinding = Assert.Single(
                window.KeyBindings,
                binding => binding.Gesture is KeyGesture gesture
                    && gesture.Key == Key.O
                    && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift));
            Assert.NotNull(fullOffBinding.Command);
            fullOffBinding.Command!.Execute(null);
            Assert.NotNull(window.ViewModel.ToggleFullOffCommand.ExecutionTask);
            await window.ViewModel.ToggleFullOffCommand.ExecutionTask!;

            var fullOffState = await service.GetStateAsync();
            Assert.True(fullOffState.IsFullOff);
        }
        finally
        {
            await window.DisposeAsync();
        }
    }

    [Fact]
    public async Task RapidMuteClicksDoNotQueueAStaleDuplicateToggle()
    {
        await using var service = new TestAudioControlService { BlockChannelMute = true };
        await using var viewModel = new MainWindowViewModel(service);
        await viewModel.InitializeAsync();
        var game = Assert.Single(viewModel.Channels, channel => channel.ChannelId == "game");

        game.ToggleMuteCommand.Execute(null);
        var firstExecution = game.ToggleMuteCommand.ExecutionTask;
        Assert.NotNull(firstExecution);
        await service.ChannelMuteStarted.Task;

        Assert.False(game.ToggleMuteCommand.CanExecute(null));
        game.ToggleMuteCommand.Execute(null);
        Assert.Equal(1, service.ChannelMuteCalls);

        service.ReleaseChannelMute.TrySetResult(true);
        await firstExecution!;

        Assert.Equal(1, service.ChannelMuteCalls);
        var state = await service.GetStateAsync();
        var gameState = Assert.Single(state.Channels, channel => channel.ChannelId == "game");
        Assert.True(game.IsMuted, viewModel.StatusMessage);
        Assert.True(gameState.IsMuted);
        Assert.False(game.IsMuteBusy);
    }

    [Fact]
    public async Task DisposeAsyncDrainsBlockedChannelMutationBeforeDisposingBackend()
    {
        await using var service = new TestAudioControlService { BlockChannelMute = true };
        await using var viewModel = new MainWindowViewModel(service);
        await viewModel.InitializeAsync();
        var game = Assert.Single(viewModel.Channels, channel => channel.ChannelId == "game");

        game.ToggleMuteCommand.Execute(null);
        var channelOperation = game.ToggleMuteCommand.ExecutionTask;
        Assert.NotNull(channelOperation);
        await service.ChannelMuteStarted.Task;

        var disposal = viewModel.DisposeAsync().AsTask();

        Assert.False(disposal.IsCompleted);

        service.ReleaseChannelMute.TrySetResult(true);
        await channelOperation!;
        await disposal;

        Assert.True(service.WasDisposed);
        Assert.Equal(0, service.OperationsAfterDispose);
        Assert.True(game.IsMuted);
    }

    private sealed class TestAudioControlService : IAudioControlService
    {
        private readonly DemoAudioControlService _inner = new();

        public bool BlockFullMute { get; set; }

        public bool BlockChannelMute { get; set; }

        public bool FailMutations { get; set; }

        public int RefreshFailuresRemaining { get; set; }

        public int RefreshCalls { get; private set; }

        public TaskCompletionSource<bool> FullMuteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseFullMute { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ChannelMuteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseChannelMute { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ChannelMuteCalls { get; private set; }

        public int OperationsAfterDispose { get; private set; }

        public bool WasDisposed => Volatile.Read(ref _isDisposed) != 0;

        private int _isDisposed;

        public bool IsDemoBackend => true;

        public Task<AudioControlState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            if (RefreshFailuresRemaining > 0)
            {
                RefreshFailuresRemaining--;
                return Task.FromException<AudioControlState>(new InvalidOperationException("transient refresh failure"));
            }

            return _inner.GetStateAsync(cancellationToken);
        }

        public Task<AudioControlState> SetChannelVolumeAsync(string channelId, float volume, CancellationToken cancellationToken = default)
            => ExecuteMutation(() => _inner.SetChannelVolumeAsync(channelId, volume, cancellationToken));

        public async Task<AudioControlState> SetChannelMuteAsync(string channelId, bool isMuted, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            if (FailMutations)
            {
                throw new InvalidOperationException("mutation failure");
            }

            ChannelMuteCalls++;
            ChannelMuteStarted.TrySetResult(true);
            if (BlockChannelMute)
            {
                await ReleaseChannelMute.Task.WaitAsync(cancellationToken);
            }

            return await _inner.SetChannelMuteAsync(channelId, isMuted, cancellationToken);
        }

        public async Task<AudioControlState> SetFullMuteAsync(bool isMuted, CancellationToken cancellationToken = default)
        {
            if (FailMutations)
            {
                throw new InvalidOperationException("mutation failure");
            }

            FullMuteStarted.TrySetResult(true);
            if (BlockFullMute)
            {
                await ReleaseFullMute.Task.WaitAsync(cancellationToken);
            }

            return await _inner.SetFullMuteAsync(isMuted, cancellationToken);
        }

        public Task<AudioControlState> SetFullOffAsync(bool isOff, CancellationToken cancellationToken = default)
            => ExecuteMutation(() => _inner.SetFullOffAsync(isOff, cancellationToken));

        public Task<AudioControlState> SetSelectedOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
            => ExecuteMutation(() => _inner.SetSelectedOutputDeviceAsync(deviceId, cancellationToken));

        public Task<AudioControlState> SetSelectedInputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
            => ExecuteMutation(() => _inner.SetSelectedInputDeviceAsync(deviceId, cancellationToken));

        public Task<AudioControlState> SetChannelOutputDeviceAsync(string channelId, string deviceId, CancellationToken cancellationToken = default)
            => ExecuteMutation(() => _inner.SetChannelOutputDeviceAsync(channelId, deviceId, cancellationToken));

        public Task<AudioControlState> SetChannelEqProfileAsync(string channelId, string profileId, CancellationToken cancellationToken = default)
            => ExecuteMutation(() => _inner.SetChannelEqProfileAsync(channelId, profileId, cancellationToken));

        public async ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _isDisposed, 1);
            await _inner.DisposeAsync();
        }

        private void EnsureNotDisposed()
        {
            if (WasDisposed)
            {
                OperationsAfterDispose++;
                throw new ObjectDisposedException(nameof(TestAudioControlService));
            }
        }

        private Task<AudioControlState> ExecuteMutation(Func<Task<AudioControlState>> mutation)
            => FailMutations
                ? Task.FromException<AudioControlState>(new InvalidOperationException("mutation failure"))
                : mutation();
    }
}
