using System.Text.Json;
using CleanChannel.Contracts;
using Xunit;

namespace CleanChannel.Core.Tests;

public sealed class AudioContractSerializationTests
{
    [Fact]
    public void DeviceContractUsesStableStringEnumValuesAndRoundTrips()
    {
        var device = new AudioDeviceDto(
            "device-id",
            "Headphones",
            AudioDeviceDirection.Render,
            IsDefault: true,
            IsAvailable: true);

        var json = JsonSerializer.Serialize(
            device,
            CleanChannelJsonContext.Default.AudioDeviceDto);
        var roundTripped = JsonSerializer.Deserialize(
            json,
            CleanChannelJsonContext.Default.AudioDeviceDto);

        Assert.Contains("\"direction\":\"Render\"", json);
        Assert.Equal(device, roundTripped);
    }
}
