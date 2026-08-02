using CleanChannel.Core.Runtime;
using Xunit;

namespace CleanChannel.Core.Tests;

public sealed class DemoAudioControlServiceTests
{
    [Fact]
    public async Task InitialStateIsExplicitlyDegradedAndContainsMvpChannels()
    {
        await using var service = new DemoAudioControlService();

        var state = await service.GetStateAsync();

        Assert.Equal(AudioHostReadiness.Degraded, state.Readiness);
        Assert.False(state.IsProcessingAvailable);
        Assert.Contains("No audio is being processed", state.StatusMessage);
        Assert.Equal(4, state.Channels.Count);
        Assert.Equal("sound", state.Channels[0].ChannelId);
        Assert.Equal("chat", state.Channels[1].ChannelId);
        Assert.Equal("game", state.Channels[2].ChannelId);
        Assert.Equal("mic", state.Channels[3].ChannelId);
    }

    [Fact]
    public async Task ChannelAndGlobalControlsPersistAcrossStateReads()
    {
        await using var service = new DemoAudioControlService();

        await service.SetChannelVolumeAsync("game", 0.42f);
        await service.SetChannelMuteAsync("game", true);
        await service.SetFullMuteAsync(true);
        await service.SetFullOffAsync(true);

        var state = await service.GetStateAsync();
        var game = Assert.Single(state.Channels, channel => channel.ChannelId == "game");

        Assert.Equal(0.42f, game.Volume);
        Assert.True(game.IsMuted);
        Assert.True(state.IsFullMute);
        Assert.True(state.IsFullOff);
    }

    [Fact]
    public async Task InvalidVolumeAndDeviceAreRejected()
    {
        await using var service = new DemoAudioControlService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetChannelVolumeAsync("sound", 2));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetSelectedOutputDeviceAsync("missing-device"));
    }
}
