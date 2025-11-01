namespace MotionMark.SkiaSharp.AvaloniaApp.Rendering;

public readonly record struct FrameStats(
    int Complexity,
    int ElementCount,
    double FrameTimeMilliseconds,
    double FramesPerSecond);
