using System;
using MotionMark.SkiaSharp.Uno.Rendering;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace MotionMark.SkiaSharp.Uno.Controls;

public sealed class MotionMarkCanvas : SKXamlCanvas
{
    public static readonly DependencyProperty ComplexityProperty =
        DependencyProperty.Register(
            nameof(Complexity),
            typeof(int),
            typeof(MotionMarkCanvas),
            new PropertyMetadata(8, OnComplexityChanged));

    public static readonly DependencyProperty UseMultithreadedRenderingProperty =
        DependencyProperty.Register(
            nameof(UseMultithreadedRendering),
            typeof(bool),
            typeof(MotionMarkCanvas),
            new PropertyMetadata(!OperatingSystem.IsBrowser(), OnRenderModeChanged));

    private readonly MotionMarkScene _scene = new();
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };
    private TimeSpan _lastFrameTimestamp;
    private double _statsAccumulatorMs;
    private int _statsFrameCount;
    private bool _renderFailed;
    private string? _lastRenderError;

    public event EventHandler<FrameStats>? FrameStatsUpdated;

    public MotionMarkCanvas()
    {
        IgnorePixelScaling = true;

        _timer.Tick += OnTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    public int Complexity
    {
        get => (int)GetValue(ComplexityProperty);
        set => SetValue(ComplexityProperty, Math.Clamp(value, 0, 24));
    }

    public bool UseMultithreadedRendering
    {
        get => (bool)GetValue(UseMultithreadedRenderingProperty);
        set => SetValue(UseMultithreadedRenderingProperty, value);
    }

    private static void OnComplexityChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is MotionMarkCanvas surface)
        {
            surface._scene.SetComplexity(surface.Complexity);
            surface.Invalidate();
        }
    }

    private static void OnRenderModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is MotionMarkCanvas surface)
        {
            surface._renderFailed = false;
            surface._lastRenderError = null;
            surface.Invalidate();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scene.SetComplexity(Complexity);
        ResetFrameStats();
        _renderFailed = false;
        _timer.Start();
        Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        ResetFrameStats();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Invalidate();
    }

    private void OnTimerTick(object? sender, object e)
    {
        if (_renderFailed)
            return;

        Invalidate();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        try
        {
            int width = e.Info.Width;
            int height = e.Info.Height;
            if (width <= 0 || height <= 0)
                return;

            _scene.Render(e.Surface.Canvas, width, height);
            e.Surface.Canvas.Flush();
            UpdateFrameStats();

            _renderFailed = false;
        }
        catch (Exception ex)
        {
            LogRenderError("painting SKXamlCanvas surface", ex);
            _renderFailed = true;
        }
    }

    private void UpdateFrameStats()
    {
        TimeSpan now = TimeSpan.FromTicks(Environment.TickCount64 * TimeSpan.TicksPerMillisecond);
        if (_lastFrameTimestamp != TimeSpan.Zero)
        {
            double deltaMs = (now - _lastFrameTimestamp).TotalMilliseconds;
            if (deltaMs > 0 && deltaMs < 250)
            {
                _statsAccumulatorMs += deltaMs;
                _statsFrameCount++;

                const double statsWindowMs = 500.0;
                if (_statsAccumulatorMs >= statsWindowMs && _statsFrameCount > 0)
                {
                    double averageFrameMs = _statsAccumulatorMs / _statsFrameCount;
                    double fps = averageFrameMs > 0 ? 1000.0 / averageFrameMs : 0;
                    var stats = new FrameStats(Complexity, _scene.ElementCount, averageFrameMs, fps);
                    FrameStatsUpdated?.Invoke(this, stats);
                    _statsAccumulatorMs = 0;
                    _statsFrameCount = 0;
                }
            }
        }

        _lastFrameTimestamp = now;
    }

    private void ResetFrameStats()
    {
        _lastFrameTimestamp = TimeSpan.Zero;
        _statsAccumulatorMs = 0;
        _statsFrameCount = 0;
    }

    private void LogRenderError(string stage, Exception exception)
    {
        Exception root = exception.GetBaseException();
        string detail = root == exception ? root.ToString() : $"{root}{Environment.NewLine}{exception}";
        string message = $"[MotionMarkCanvas] Error while {stage}: {detail}";
        if (message == _lastRenderError)
            return;

        _lastRenderError = message;
        Console.Error.WriteLine(message);
    }

    ~MotionMarkCanvas()
    {
        _scene.Dispose();
    }
}
