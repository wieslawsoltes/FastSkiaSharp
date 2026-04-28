namespace MotionMark.SkiaSharp.Uno.Rendering;

public readonly record struct FrameStats(
    int Complexity,
    int ElementCount,
    double FrameTimeMilliseconds,
    double FramesPerSecond);
