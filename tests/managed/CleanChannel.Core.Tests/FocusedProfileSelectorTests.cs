using CleanChannel.Core.Profiles;
using Xunit;

namespace CleanChannel.Core.Tests;

public sealed class FocusedProfileSelectorTests
{
    [Fact]
    public void SelectReturnsNoProfileWhenProcessDoesNotMatch()
    {
        var profiles = new[]
        {
            new FocusedProfile("valorant", "Valorant", "VALORANT-Win64-Shipping", 100),
        };

        var selected = FocusedProfileSelector.Select("spotify", profiles);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectReturnsHighestPriorityMatchingProfile()
    {
        var profiles = new[]
        {
            new FocusedProfile("default", "Default game", "valorant", 10),
            new FocusedProfile("competitive", "Competitive game", "valorant", 100),
        };

        var selected = FocusedProfileSelector.Select("VALORANT", profiles);

        Assert.NotNull(selected);
        Assert.Equal("competitive", selected.ProfileId);
    }

    [Fact]
    public void SelectResolvesPriorityTiesByProfileIdIndependentOfInputOrder()
    {
        var profiles = new[]
        {
            new FocusedProfile("zulu", "Zulu", "valorant", 50),
            new FocusedProfile("alpha", "Alpha", "valorant", 50),
        };

        var selected = FocusedProfileSelector.Select("valorant", profiles);

        Assert.NotNull(selected);
        Assert.Equal("alpha", selected.ProfileId);
    }

    [Fact]
    public void SelectIgnoresDisabledMatchingProfiles()
    {
        var profiles = new[]
        {
            new FocusedProfile("disabled", "Disabled", "valorant", 100, IsEnabled: false),
            new FocusedProfile("enabled", "Enabled", "valorant", 10),
        };

        var selected = FocusedProfileSelector.Select("valorant", profiles);

        Assert.NotNull(selected);
        Assert.Equal("enabled", selected.ProfileId);
    }

    [Fact]
    public void SelectDoesNotMatchBlankForegroundOrProfileProcessNames()
    {
        var profiles = new[]
        {
            new FocusedProfile("blank", "Blank", "", 100),
        };

        var selected = FocusedProfileSelector.Select(null, profiles);

        Assert.Null(selected);
    }
}
