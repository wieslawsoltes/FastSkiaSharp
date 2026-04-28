using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionMark.SkiaSharp.Uno;

internal sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private int _complexity = 8;
    private int _elementCount;
    private double _frameTimeMs;
    private double _fps;
    private bool _useMultithreaded = !OperatingSystem.IsBrowser();

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Complexity
    {
        get => _complexity;
        set
        {
            int clamped = Math.Clamp(value, 0, 24);
            if (_complexity != clamped)
            {
                _complexity = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ComplexityValue));
                OnPropertyChanged(nameof(ComplexityText));
            }
        }
    }

    public double ComplexityValue
    {
        get => _complexity;
        set => Complexity = (int)Math.Round(value);
    }

    public string ComplexityText => $"x{_complexity}";

    public bool IsMultithreadedToggleEnabled => !OperatingSystem.IsBrowser();

    public int ElementCount
    {
        get => _elementCount;
        set
        {
            if (_elementCount != value)
            {
                _elementCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ElementCountText));
            }
        }
    }

    public string ElementCountText => $"Elements: {_elementCount:N0}";

    public double FrameTimeMilliseconds
    {
        get => _frameTimeMs;
        set
        {
            if (Math.Abs(_frameTimeMs - value) > 0.0001)
            {
                _frameTimeMs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FrameTimeText));
            }
        }
    }

    public string FrameTimeText => $"Frame: {_frameTimeMs:F2} ms";

    public double FramesPerSecond
    {
        get => _fps;
        set
        {
            if (Math.Abs(_fps - value) > 0.0001)
            {
                _fps = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FramesPerSecondText));
            }
        }
    }

    public string FramesPerSecondText => $"FPS: {_fps:F1}";

    public bool UseMultithreadedRendering
    {
        get => _useMultithreaded;
        set
        {
            if (_useMultithreaded != value)
            {
                _useMultithreaded = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
