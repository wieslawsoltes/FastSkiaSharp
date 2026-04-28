# MotionMark SkiaSharp Uno

Uno Platform port of the MotionMark-style SkiaSharp sample.

The sample targets `net10.0-desktop` and renders with `SkiaSharp`
`4.147.0-preview.1.1` into an offscreen buffer presented through Uno
`WriteableBitmap`.

`SkiaSharp.Views.Uno.WinUI` 4 preview was tested but currently crashes on
the Uno desktop head in the native `sk_pathbuilder_detach_path` finalizer path,
including with a blank `SKXamlCanvas`. The sample therefore avoids that view
package while still exercising the SkiaSharp 4 preview renderer.
