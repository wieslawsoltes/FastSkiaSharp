# MotionMark SkiaSharp Uno

Uno Platform port of the MotionMark-style SkiaSharp sample.

The sample targets `net10.0-desktop` and renders with `SkiaSharp`
`4.147.0-preview.1.1` through `SkiaSharp.Views.Uno.WinUI`
`4.147.0-preview.1.1`.

The drawing surface derives from `SKXamlCanvas` and renders directly in
`OnPaintSurface` to avoid the extra offscreen `WriteableBitmap` copy path. The
scene uses `SKPathBuilder.Detach()` and `DrawPath` so the sample exercises the
faster SkiaSharp 4 path rendering API.

The current SkiaSharp 4 preview can still hit a native
`sk_pathbuilder_detach_path` crash on the Uno desktop head. The sample keeps
the `SKXamlCanvas` path active for performance testing and issue reporting.
