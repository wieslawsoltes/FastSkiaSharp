namespace MotionMark.SkiaSharp.Uno;

public sealed partial class MainPage : Page
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ManagedSurface.FrameStatsUpdated += OnFrameStatsUpdated;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ManagedSurface.FrameStatsUpdated -= OnFrameStatsUpdated;
    }

    private void OnFrameStatsUpdated(object? sender, Rendering.FrameStats stats)
    {
        _viewModel.Complexity = stats.Complexity;
        _viewModel.ElementCount = stats.ElementCount;
        _viewModel.FrameTimeMilliseconds = stats.FrameTimeMilliseconds;
        _viewModel.FramesPerSecond = stats.FramesPerSecond;
    }
}
