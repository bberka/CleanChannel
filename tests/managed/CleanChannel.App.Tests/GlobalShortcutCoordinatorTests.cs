using Avalonia.Input;
using CleanChannel.App.Shortcuts;
using CleanChannel.App.ViewModels;
using CleanChannel.Core.Runtime;
using Xunit;

namespace CleanChannel.App.Tests;

public sealed class GlobalShortcutCoordinatorTests
{
    [Fact]
    public async Task FakeGlobalShortcutCallbacksDriveFullMuteAndFullOffCommands()
    {
        await using var audioService = new DemoAudioControlService();
        await using var viewModel = new MainWindowViewModel(audioService);
        await viewModel.InitializeAsync();
        using var shortcutService = new FakeGlobalShortcutService();
        await using var coordinator = new GlobalShortcutCoordinator(shortcutService, viewModel);

        var results = coordinator.Register(GlobalShortcutDefaults.All);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(result.IsRegistered, result.ErrorMessage));
        Assert.Equal(
            Key.M,
            shortcutService.Registered[GlobalShortcutDefaults.FullMuteId].Definition.Key);
        Assert.Equal(
            Key.O,
            shortcutService.Registered[GlobalShortcutDefaults.FullOffId].Definition.Key);

        shortcutService.Trigger(GlobalShortcutDefaults.FullMuteId);
        Assert.NotNull(viewModel.ToggleFullMuteCommand.ExecutionTask);
        await viewModel.ToggleFullMuteCommand.ExecutionTask!;

        shortcutService.Trigger(GlobalShortcutDefaults.FullOffId);
        Assert.NotNull(viewModel.ToggleFullOffCommand.ExecutionTask);
        await viewModel.ToggleFullOffCommand.ExecutionTask!;

        var state = await audioService.GetStateAsync();
        Assert.True(state.IsFullMute);
        Assert.True(state.IsFullOff);
        Assert.True(viewModel.IsFullMute);
        Assert.True(viewModel.IsFullOff);
    }

    [Fact]
    public async Task RegistrationFailureIsReportedWithoutPreventingOtherShortcuts()
    {
        using var service = new FakeGlobalShortcutService
        {
            FailureId = GlobalShortcutDefaults.FullMuteId,
        };
        await using var audioService = new DemoAudioControlService();
        await using var viewModel = new MainWindowViewModel(audioService);
        await using var coordinator = new GlobalShortcutCoordinator(service, viewModel);

        var results = coordinator.Register(GlobalShortcutDefaults.All);

        Assert.False(results.Single(result => result.ShortcutId == GlobalShortcutDefaults.FullMuteId).IsRegistered);
        Assert.True(results.Single(result => result.ShortcutId == GlobalShortcutDefaults.FullOffId).IsRegistered);
    }

    [Fact]
    public async Task DisposeAsyncStopsCallbacksAndDrainsBlockedCommandBeforeViewModelDisposal()
    {
        await using var audioService = new BlockingAudioControlService();
        await using var viewModel = new MainWindowViewModel(audioService);
        await viewModel.InitializeAsync();
        using var shortcutService = new FakeGlobalShortcutService();
        var coordinator = new GlobalShortcutCoordinator(shortcutService, viewModel);

        try
        {
            coordinator.Register(
            [
                GlobalShortcutDefaults.All.Single(
                    definition => definition.Id == GlobalShortcutDefaults.FullMuteId),
            ]);

            shortcutService.Trigger(GlobalShortcutDefaults.FullMuteId);
            await audioService.FullMuteStarted.Task;

            var disposal = coordinator.DisposeAsync().AsTask();

            Assert.False(disposal.IsCompleted);
            Assert.False(shortcutService.TryTrigger(GlobalShortcutDefaults.FullMuteId));

            audioService.ReleaseFullMute.TrySetResult(true);
            await disposal;
        }
        finally
        {
            audioService.ReleaseFullMute.TrySetResult(true);
            await coordinator.DisposeAsync();
        }
    }

    private sealed class FakeGlobalShortcutService : IGlobalShortcutService
    {
        public Dictionary<string, RegisteredShortcut> Registered { get; } = new(StringComparer.Ordinal);

        public string? FailureId { get; init; }

        public bool IsSupported => true;

        public GlobalShortcutRegistrationResult TryRegister(
            GlobalShortcutDefinition definition,
            Action callback)
        {
            if (string.Equals(definition.Id, FailureId, StringComparison.Ordinal))
            {
                return new GlobalShortcutRegistrationResult(
                    definition.Id,
                    false,
                    "test conflict");
            }

            Registered.Add(definition.Id, new RegisteredShortcut(definition, callback));
            return new GlobalShortcutRegistrationResult(definition.Id, true, null);
        }

        public void Trigger(string shortcutId) => Registered[shortcutId].Callback();

        public bool TryTrigger(string shortcutId)
        {
            if (!Registered.TryGetValue(shortcutId, out var shortcut))
            {
                return false;
            }

            shortcut.Callback();
            return true;
        }

        public void UnregisterAll() => Registered.Clear();

        public void Dispose() => UnregisterAll();

        public sealed record RegisteredShortcut(
            GlobalShortcutDefinition Definition,
            Action Callback);
    }

    private sealed class BlockingAudioControlService : IAudioControlService
    {
        private readonly DemoAudioControlService _inner = new();

        public TaskCompletionSource<bool> FullMuteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseFullMute { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsDemoBackend => true;

        public Task<AudioControlState> GetStateAsync(CancellationToken cancellationToken = default)
            => _inner.GetStateAsync(cancellationToken);

        public Task<AudioControlState> SetChannelVolumeAsync(
            string channelId,
            float volume,
            CancellationToken cancellationToken = default)
            => _inner.SetChannelVolumeAsync(channelId, volume, cancellationToken);

        public Task<AudioControlState> SetChannelMuteAsync(
            string channelId,
            bool isMuted,
            CancellationToken cancellationToken = default)
            => _inner.SetChannelMuteAsync(channelId, isMuted, cancellationToken);

        public async Task<AudioControlState> SetFullMuteAsync(
            bool isMuted,
            CancellationToken cancellationToken = default)
        {
            FullMuteStarted.TrySetResult(true);
            await ReleaseFullMute.Task.WaitAsync(cancellationToken);
            return await _inner.SetFullMuteAsync(isMuted, cancellationToken);
        }

        public Task<AudioControlState> SetFullOffAsync(
            bool isOff,
            CancellationToken cancellationToken = default)
            => _inner.SetFullOffAsync(isOff, cancellationToken);

        public Task<AudioControlState> SetSelectedOutputDeviceAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
            => _inner.SetSelectedOutputDeviceAsync(deviceId, cancellationToken);

        public Task<AudioControlState> SetSelectedInputDeviceAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
            => _inner.SetSelectedInputDeviceAsync(deviceId, cancellationToken);

        public Task<AudioControlState> SetChannelOutputDeviceAsync(
            string channelId,
            string deviceId,
            CancellationToken cancellationToken = default)
            => _inner.SetChannelOutputDeviceAsync(channelId, deviceId, cancellationToken);

        public Task<AudioControlState> SetChannelEqProfileAsync(
            string channelId,
            string profileId,
            CancellationToken cancellationToken = default)
            => _inner.SetChannelEqProfileAsync(channelId, profileId, cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
