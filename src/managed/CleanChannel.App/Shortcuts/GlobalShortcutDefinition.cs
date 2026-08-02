using Avalonia.Input;

namespace CleanChannel.App.Shortcuts;

public sealed record GlobalShortcutDefinition(
    string Id,
    Key Key,
    KeyModifiers Modifiers,
    string DisplayName);

public sealed record GlobalShortcutRegistrationResult(
    string ShortcutId,
    bool IsRegistered,
    string? ErrorMessage);

public static class GlobalShortcutDefaults
{
    public const string FullMuteId = "full-mute";
    public const string FullOffId = "full-off";

    public static IReadOnlyList<GlobalShortcutDefinition> All { get; } =
    [
        new(FullMuteId, Key.M, KeyModifiers.Control | KeyModifiers.Shift, "Full mute"),
        new(FullOffId, Key.O, KeyModifiers.Control | KeyModifiers.Shift, "Full off / restore"),
    ];
}
