# MotionMark Native Sample

This native C++ sample replicates a MotionMark-style scene using Skia's
`sk_app` windowing layer. It renders hundreds of animated paths inside a
desktop window and reports a running FPS value in the window title bar.

## Building

1. Sync Skia's third-party dependencies (only required the first time) and download the GN/Ninja
   toolchain binaries:

   ```bash
   cd extern/skia
   python3 tools/git-sync-deps
   python3 bin/fetch-gn
   python3 bin/fetch-ninja
   curl -L https://github.com/bazelbuild/bazelisk/releases/latest/download/bazelisk-darwin-arm64 -o "$HOME/bin/bazelisk"
   chmod +x "$HOME/bin/bazelisk"
   ```

   Ensure `$HOME/bin` is on your shell `PATH` (for example, add `export PATH="$HOME/bin:$PATH"` to `~/.zshrc`).

2. Generate a build directory that enables the native sample (defaults to a release build):

   ```bash
   bin/gn gen out/motionmark \
     --args='is_debug=false skia_use_gl=true skia_use_metal=false skia_use_icu=false skia_use_harfbuzz=false'
   ```

   Pass `is_debug=true` if you need a debug build instead.

3. Compile the executable and shared FFI library:

   ```bash
   third_party/ninja/ninja -C out/motionmark motionmark_native motionmark_ffi
   ```

4. Run the sample (from the `extern/skia` directory):

   ```bash
   out/motionmark/motionmark_native
   ```

The executable opens a resizable window that continuously animates the scene.

Optional flags:

- `--complexity=<0-24>` adjusts the curve count to mimic MotionMark’s complexity slider.

### Enabling the experimental Vello backend

If you want to exercise Graphite with the Vello shading backend, install
[bazelisk](https://github.com/bazelbuild/bazelisk) so it is on your `PATH`, then regenerate the
build with:

```bash
bin/gn gen out/motionmark \
  --args='is_debug=false skia_use_gl=true skia_use_metal=true skia_enable_graphite=true skia_enable_vello_shaders=true skia_use_icu=false skia_use_harfbuzz=false min_macos_version="11.0"'
```

When compiling with Vello enabled, make sure `PATH=$PWD/bin:$PATH third_party/ninja/ninja -C out/motionmark motionmark_native motionmark_ffi`
is used so that Skia's Bazel toolchain helpers (including `bazelisk`) are visible.
