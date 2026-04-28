using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using MotionMark.SkiaSharp.Uno.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;

namespace MotionMark.SkiaSharp.Uno.Controls;

public sealed class MotionMarkCanvas : UserControl
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
    private readonly Image _image = new()
    {
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };
    private TimeSpan _lastFrameTimestamp;
    private double _statsAccumulatorMs;
    private int _statsFrameCount;
    private WriteableBitmap? _bitmap;
    private byte[] _pixels = [];
    private int _pixelWidth;
    private int _pixelHeight;
    private bool _renderFailed;
    private string? _lastRenderError;

    public event EventHandler<FrameStats>? FrameStatsUpdated;

    public MotionMarkCanvas()
    {
        Content = _image;
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
            surface.RenderFrame();
        }
    }

    private static void OnRenderModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is MotionMarkCanvas surface)
        {
            surface._renderFailed = false;
            surface._lastRenderError = null;
            surface.RenderFrame();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scene.SetComplexity(Complexity);
        ResetFrameStats();
        _renderFailed = false;
        _timer.Start();
        RenderFrame();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        ResetFrameStats();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _pixelWidth = 0;
        _pixelHeight = 0;
        RenderFrame();
    }

    private void OnTimerTick(object? sender, object e)
    {
        if (_renderFailed)
            return;

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
        RenderFrame();
    }

    private void RenderFrame()
    {
        try
        {
            double scale = XamlRoot?.RasterizationScale ?? 1.0;
            int width = Math.Max(0, (int)Math.Ceiling(ActualWidth * scale));
            int height = Math.Max(0, (int)Math.Ceiling(ActualHeight * scale));
            if (width <= 0 || height <= 0)
                return;

            EnsureBitmap(width, height);

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            GCHandle pixelsHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
            try
            {
                using var surface = SKSurface.Create(info, pixelsHandle.AddrOfPinnedObject(), info.RowBytes);
                if (surface is null)
                    throw new InvalidOperationException("Unable to create SkiaSharp render surface.");

                _scene.Render(surface.Canvas, width, height);
                surface.Canvas.Flush();
            }
            finally
            {
                pixelsHandle.Free();
            }

            using Stream stream = _bitmap!.PixelBuffer.AsStream();
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(_pixels, 0, _pixels.Length);
            _bitmap.Invalidate();

            _renderFailed = false;
        }
        catch (Exception ex)
        {
            LogRenderError("rendering frame", ex);
            _renderFailed = true;
        }
    }

    private void ResetFrameStats()
    {
        _lastFrameTimestamp = TimeSpan.Zero;
        _statsAccumulatorMs = 0;
        _statsFrameCount = 0;
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is not null && _pixelWidth == width && _pixelHeight == height)
            return;

        _pixelWidth = width;
        _pixelHeight = height;
        _pixels = new byte[width * height * 4];
        _bitmap = new WriteableBitmap(width, height);
        _image.Source = _bitmap;
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
