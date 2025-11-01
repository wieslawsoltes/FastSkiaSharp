using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using MotionMark.SkiaSharp.AvaloniaApp.Controls;
using MotionMark.SkiaSharp.AvaloniaApp.Rendering;

namespace MotionMark.SkiaSharp.AvaloniaApp;

public partial class MainView : UserControl
{
    private readonly MainWindowViewModel _viewModel = new();
    private MotionMarkSurface? _managedSurface;
    private MotionMarkNativeSurface? _nativeSurface;
    private bool _managedStatsSubscribed;
    private bool _nativeStatsSubscribed;
    private TabControl? _tabControl;

    public MainView()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _tabControl = this.FindControl<TabControl>("RendererTabs");
        if (_tabControl is not null)
        {
            _tabControl.SelectionChanged += OnTabSelectionChanged;
        }
    }

    private void OnFrameStatsUpdated(object? sender, FrameStats stats)
    {
        _viewModel.Complexity = stats.Complexity;
        _viewModel.ElementCount = stats.ElementCount;
        _viewModel.FrameTimeMilliseconds = stats.FrameTimeMilliseconds;
        _viewModel.FramesPerSecond = stats.FramesPerSecond;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        HookSurfaceHandlers();

        if (e.Root is TopLevel topLevel)
        {
            topLevel.RendererDiagnostics.DebugOverlays =
                RendererDebugOverlays.Fps |
                RendererDebugOverlays.LayoutTimeGraph |
                RendererDebugOverlays.RenderTimeGraph;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeSurfaces();

        base.OnDetachedFromVisualTree(e);
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        => HookSurfaceHandlers();

    private void HookSurfaceHandlers()
    {
        _managedSurface ??= this.FindControl<MotionMarkSurface>("ManagedSurface");
        if (_managedSurface is not null && !_managedStatsSubscribed)
        {
            _managedSurface.FrameStatsUpdated += OnFrameStatsUpdated;
            _managedStatsSubscribed = true;
        }

        _nativeSurface ??= this.FindControl<MotionMarkNativeSurface>("NativeSurface");
        if (_nativeSurface is not null && !_nativeStatsSubscribed)
        {
            _nativeSurface.FrameStatsUpdated += OnFrameStatsUpdated;
            _nativeStatsSubscribed = true;
        }
    }

    private void UnsubscribeSurfaces()
    {
        if (_managedSurface is not null && _managedStatsSubscribed)
        {
            _managedSurface.FrameStatsUpdated -= OnFrameStatsUpdated;
            _managedStatsSubscribed = false;
        }

        if (_nativeSurface is not null && _nativeStatsSubscribed)
        {
            _nativeSurface.FrameStatsUpdated -= OnFrameStatsUpdated;
            _nativeStatsSubscribed = false;
        }
    }
}
