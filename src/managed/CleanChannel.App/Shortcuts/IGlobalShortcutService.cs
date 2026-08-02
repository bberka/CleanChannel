namespace CleanChannel.App.Shortcuts;

public interface IGlobalShortcutService : IDisposable
{
    bool IsSupported { get; }

    GlobalShortcutRegistrationResult TryRegister(
        GlobalShortcutDefinition definition,
        Action callback);

    void UnregisterAll();
}
