using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Platform;
using Avalonia.VisualTree;
using MotionMark.SkiaSharp.AvaloniaApp.Rendering;
using SkiaSharp;

namespace MotionMark.SkiaSharp.AvaloniaApp.Controls;

/// <summary>
/// Renders the MotionMark scene directly using SkiaSharp leased from Avalonia.
/// </summary>
public sealed class MotionMarkSurface : Control
{
    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<MotionMarkSurface, int>(
            nameof(Complexity),
            8,
            coerce: (_, value) => Math.Clamp(value, 0, 24));

    public static readonly StyledProperty<bool> UseMultithreadedRenderingProperty =
        AvaloniaProperty.Register<MotionMarkSurface, bool>(
            nameof(UseMultithreadedRendering),
            true);

    private readonly MotionMarkScene _scene = new();
    private bool _frameRequested;
    private bool _isAttached;
    private TimeSpan? _lastFrameTimestamp;
    private double _statsAccumulatorMs;
    private int _statsFrameCount;
    private bool _renderFailed;
    private string? _lastRenderError;

    public event EventHandler<FrameStats>? FrameStatsUpdated;

    public MotionMarkSurface()
    {
        ClipToBounds = true;
        UseMultithreadedRendering = !OperatingSystem.IsBrowser();
    }

    public int Complexity
    {
        get => GetValue(ComplexityProperty);
        set => SetValue(ComplexityProperty, value);
    }

    public bool UseMultithreadedRendering
    {
        get => GetValue(UseMultithreadedRenderingProperty);
        set => SetValue(UseMultithreadedRenderingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ComplexityProperty)
        {
            _scene.SetComplexity(Complexity);
        }
        else if (change.Property == UseMultithreadedRenderingProperty)
        {
            // We could add background recording using Task.Run in the future.
            _renderFailed = false;
            _lastRenderError = null;
            RequestNextFrame();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        _scene.SetComplexity(Complexity);
        RequestNextFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        _frameRequested = false;
        _lastFrameTimestamp = null;
        _statsAccumulatorMs = 0;
        _statsFrameCount = 0;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        double scaling = topLevel.RenderScaling;
        var destRect = new Rect(Bounds.Size);

        if (destRect.Width <= 0 || destRect.Height <= 0)
            return;

        PixelSize pixelSize = PixelSize.FromSize(destRect.Size, scaling);
        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
            return;

        context.Custom(new MotionMarkDrawOperation(this, destRect, pixelSize, scaling));
    }

    private void RenderFrame(ImmediateDrawingContext drawingContext, Rect destRect, PixelSize pixelSize, double scaling)
    {
        try
        {
            if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
                return;

            if (destRect.Width <= 0 || destRect.Height <= 0)
                return;

            var leaseFeature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                _renderFailed = true;
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas is null)
            {
                _renderFailed = true;
                return;
            }

            canvas.Save();
            try
            {
                if (destRect.X != 0 || destRect.Y != 0)
                {
                    canvas.Translate((float)destRect.X, (float)destRect.Y);
                }

                canvas.ClipRect(new SKRect(0, 0, (float)destRect.Width, (float)destRect.Height));
                _scene.Render(canvas, (float)destRect.Width, (float)destRect.Height);
                _renderFailed = false;
            }
            finally
            {
                canvas.Restore();
            }
        }
        catch (Exception ex)
        {
            LogRenderError("rendering frame", ex);
            _renderFailed = true;
        }
    }

    private void RequestNextFrame()
    {
        if (!_isAttached || _frameRequested)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        _frameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _frameRequested = false;

        if (!_isAttached)
            return;

        if (_lastFrameTimestamp is TimeSpan last)
        {
            double deltaMs = (timestamp - last).TotalMilliseconds;
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

        _lastFrameTimestamp = timestamp;
        InvalidateVisual();
        if (!_renderFailed)
        {
            RequestNextFrame();
        }
    }

    private void LogRenderError(string stage, Exception exception)
    {
        Exception root = exception.GetBaseException();
        string detail = root == exception ? root.ToString() : $"{root}{Environment.NewLine}{exception}";
        string message = $"[MotionMarkSurface] Error while {stage}: {detail}";
        if (message == _lastRenderError)
            return;

        _lastRenderError = message;
        Console.Error.WriteLine(message);
    }

    ~MotionMarkSurface()
    {
        _scene.Dispose();
    }

    private sealed class MotionMarkDrawOperation : ICustomDrawOperation
    {
        private readonly MotionMarkSurface _owner;
        private readonly Rect _destRect;
        private readonly PixelSize _pixelSize;
        private readonly double _scaling;

        public MotionMarkDrawOperation(MotionMarkSurface owner, Rect destRect, PixelSize pixelSize, double scaling)
        {
            _owner = owner;
            _destRect = destRect;
            _pixelSize = pixelSize;
            _scaling = scaling;
        }

        public Rect Bounds => _destRect;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _destRect.Contains(p);

        public void Render(ImmediateDrawingContext context)
            => _owner.RenderFrame(context, _destRect, _pixelSize, _scaling);

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is MotionMarkDrawOperation op &&
                   ReferenceEquals(op._owner, _owner) &&
                   op._destRect == _destRect &&
                   op._pixelSize == _pixelSize &&
                   Math.Abs(op._scaling - _scaling) < double.Epsilon;
        }
    }
}
