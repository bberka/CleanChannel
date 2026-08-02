using CleanChannel.Core.Runtime;

namespace CleanChannel.App.ViewModels;

public sealed class EqProfileViewModel
{
    public EqProfileViewModel(EqProfile profile)
    {
        ProfileId = profile.ProfileId;
        DisplayName = profile.DisplayName;
        Description = profile.Description;
        IsDemo = profile.IsDemo;
    }

    public string ProfileId { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool IsDemo { get; }

    public override string ToString() => DisplayName;
}
