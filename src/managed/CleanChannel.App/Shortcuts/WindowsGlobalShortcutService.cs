using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using CleanChannel.App.Shortcuts;
using Microsoft.Extensions.Logging;

namespace CleanChannel.App.Shortcuts;

public sealed class WindowsGlobalShortcutService : IGlobalShortcutService
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotKey = 0x0312;

    private readonly Window _hostWindow;
    private readonly ILogger? _logger;
    private readonly Dictionary<int, RegisteredShortcut> _registeredShortcuts = new();
    private readonly Win32Properties.CustomWndProcHookCallback _wndProcHookCallback;
    private nint _windowHandle;
    private int _nextNativeId = 1;
    private bool _hookAttached;
    private bool _isDisposed;

    public WindowsGlobalShortcutService(Window hostWindow, ILogger? logger = null)
    {
        _hostWindow = hostWindow ?? throw new ArgumentNullException(nameof(hostWindow));
        _logger = logger;
        _wndProcHookCallback = OnWndProc;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public GlobalShortcutRegistrationResult TryRegister(
        GlobalShortcutDefinition definition,
        Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_isDisposed)
        {
            return Failure(definition, "The global shortcut service has already been disposed.");
        }

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            return Failure(definition, "The shortcut ID cannot be empty.");
        }

        if (!IsSupported)
        {
            return Failure(definition, "Global Windows shortcuts are not supported on this platform.");
        }

        if (_registeredShortcuts.Values.Any(shortcut =>
                string.Equals(shortcut.Definition.Id, definition.Id, StringComparison.Ordinal)))
        {
            return Failure(definition, "A shortcut with this ID is already registered.");
        }

        if (!TryGetVirtualKey(definition.Key, out var virtualKey))
        {
            return Failure(definition, $"The key '{definition.Key}' is not supported by the Windows MVP mapper.");
        }

        try
        {
            if (!TryGetWindowHandle(out var windowHandle, out var handleError))
            {
                return Failure(definition, handleError);
            }

            _windowHandle = windowHandle;
            EnsureWndProcHook();

            var nativeId = AllocateNativeId();
            if (!RegisterHotKey(
                    _windowHandle,
                    nativeId,
                    ToNativeModifiers(definition.Modifiers),
                    virtualKey))
            {
                var errorCode = Marshal.GetLastWin32Error();
                return Failure(
                    definition,
                    $"Windows rejected the shortcut. {new Win32Exception(errorCode).Message} (error {errorCode}).");
            }

            _registeredShortcuts.Add(
                nativeId,
                new RegisteredShortcut(definition, callback));

            return new GlobalShortcutRegistrationResult(definition.Id, true, null);
        }
        catch (Exception error)
        {
            if (_logger is not null)
            {
                ShortcutLogMessages.WindowsRegistrationException(_logger, error, definition.Id);
            }

            return Failure(definition, error.Message);
        }
    }

    public void UnregisterAll()
    {
        if (_isDisposed && _registeredShortcuts.Count == 0)
        {
            return;
        }

        if (IsSupported && _windowHandle != nint.Zero)
        {
            foreach (var nativeId in _registeredShortcuts.Keys.ToArray())
            {
                try
                {
                    UnregisterHotKey(_windowHandle, nativeId);
                }
                catch (Exception error)
                {
                    if (_logger is not null)
                    {
                        ShortcutLogMessages.UnregisterException(_logger, error, nativeId);
                    }
                }
            }
        }

        _registeredShortcuts.Clear();

        if (_hookAttached)
        {
            try
            {
                Win32Properties.RemoveWndProcHookCallback(_hostWindow, _wndProcHookCallback);
            }
            catch (Exception error)
            {
                if (_logger is not null)
                {
                    ShortcutLogMessages.HookCleanupException(_logger, error);
                }
            }

            _hookAttached = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        UnregisterAll();
        _isDisposed = true;
    }

    private static GlobalShortcutRegistrationResult Failure(
        GlobalShortcutDefinition definition,
        string? errorMessage)
        => new(definition.Id, false, errorMessage);

    private void EnsureWndProcHook()
    {
        if (_hookAttached)
        {
            return;
        }

        Win32Properties.AddWndProcHookCallback(_hostWindow, _wndProcHookCallback);
        _hookAttached = true;
    }

    private bool TryGetWindowHandle(out nint windowHandle, out string errorMessage)
    {
        windowHandle = nint.Zero;
        var platformHandle = _hostWindow.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == nint.Zero)
        {
            errorMessage = "The hidden shortcut host does not have a native window handle yet.";
            return false;
        }

        if (!string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"The shortcut host handle '{platformHandle.HandleDescriptor}' is not a Windows HWND.";
            return false;
        }

        windowHandle = platformHandle.Handle;
        errorMessage = string.Empty;
        return true;
    }

    private int AllocateNativeId()
    {
        while (_registeredShortcuts.ContainsKey(_nextNativeId))
        {
            _nextNativeId++;
        }

        return _nextNativeId++;
    }

    private nint OnWndProc(
        nint hWnd,
        uint message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message != WmHotKey || !_registeredShortcuts.TryGetValue(wParam.ToInt32(), out var shortcut))
        {
            return nint.Zero;
        }

        handled = true;
        try
        {
            shortcut.Callback();
        }
        catch (Exception error)
        {
            if (_logger is not null)
            {
                ShortcutLogMessages.CallbackException(_logger, error, shortcut.Definition.Id);
            }
        }

        return nint.Zero;
    }

    private static uint ToNativeModifiers(KeyModifiers modifiers)
    {
        var nativeModifiers = ModNoRepeat;
        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            nativeModifiers |= ModAlt;
        }

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            nativeModifiers |= ModControl;
        }

        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            nativeModifiers |= ModShift;
        }

        if ((modifiers & KeyModifiers.Meta) != 0)
        {
            nativeModifiers |= ModWin;
        }

        return nativeModifiers;
    }

    private static bool TryGetVirtualKey(Key key, out uint virtualKey)
    {
        var keyName = key.ToString();
        if (keyName.Length == 1 && keyName[0] is >= 'A' and <= 'Z')
        {
            virtualKey = keyName[0];
            return true;
        }

        if (keyName.Length == 2 && keyName[0] == 'D' && keyName[1] is >= '0' and <= '9')
        {
            virtualKey = keyName[1];
            return true;
        }

        if (keyName.Length > 1
            && keyName[0] == 'F'
            && uint.TryParse(keyName.AsSpan(1), out var functionNumber)
            && functionNumber is >= 1 and <= 24)
        {
            virtualKey = 0x70u + functionNumber - 1;
            return true;
        }

        virtualKey = key switch
        {
            Key.Space => 0x20u,
            Key.Escape => 0x1Bu,
            Key.Tab => 0x09u,
            Key.Enter => 0x0Du,
            Key.Back => 0x08u,
            _ => 0u,
        };

        return virtualKey != 0;
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private sealed record RegisteredShortcut(
        GlobalShortcutDefinition Definition,
        Action Callback);
}
