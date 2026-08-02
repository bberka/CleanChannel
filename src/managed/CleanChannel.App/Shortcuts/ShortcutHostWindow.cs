using Avalonia;
using Avalonia.Controls;

namespace CleanChannel.App.Shortcuts;

internal sealed class ShortcutHostWindow : Window
{
    public ShortcutHostWindow()
    {
        Width = 1;
        Height = 1;
        CanResize = false;
        Opacity = 0;
        Position = new PixelPoint(-32000, -32000);
        ShowInTaskbar = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
    }
}
