using Avalonia.Controls;
using CleanChannel.App.ViewModels;
using CleanChannel.Core.Runtime;

namespace CleanChannel.App;

public partial class MainWindow : Window, IAsyncDisposable
{
    public MainWindow()
        : this(new DemoAudioControlService())
    {
    }

    public MainWindow(IAudioControlService audioService)
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(audioService);
        DataContext = ViewModel;
        Opened += MainWindow_OnOpened;
    }

    public MainWindowViewModel ViewModel { get; }

    private async void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        Opened -= MainWindow_OnOpened;
        await ViewModel.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await ViewModel.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
