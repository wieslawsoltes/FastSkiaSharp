# MotionMark SkiaSharp Uno

Uno Platform port of the MotionMark-style SkiaSharp sample.

The sample targets `net10.0-desktop` and renders with `SkiaSharp`
`4.147.0-preview.1.1` through `SkiaSharp.Views.Uno.WinUI`
`4.147.0-preview.1.1`.

The drawing surface derives from Uno's `SKCanvasElement` and renders directly
in `RenderOverride` to avoid the extra offscreen `WriteableBitmap` copy path.

The scene still uses `SKPathBuilder.Detach()` and `DrawPath`. The current
SkiaSharp 4 preview can hit a native `sk_pathbuilder_detach_path` crash on the
Uno desktop head; update the SkiaSharp packages once the upstream fix is
published.
