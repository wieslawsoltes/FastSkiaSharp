using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace MotionMark.SkiaSharp.Uno.Rendering;

internal sealed class MotionMarkScene : IDisposable
{
    private const int GridWidth = 80;
    private const int GridHeight = 40;
    private const int QuadSegmentCount = 8;
    private const int CubicSegmentCount = 12;

    private static readonly SKColor[] s_palette =
    [
        new SKColor(0x10, 0x10, 0x10),
        new SKColor(0x80, 0x80, 0x80),
        new SKColor(0xC0, 0xC0, 0xC0),
        new SKColor(0x10, 0x10, 0x10),
        new SKColor(0x80, 0x80, 0x80),
        new SKColor(0xC0, 0xC0, 0xC0),
        new SKColor(0xE0, 0x10, 0x40),
    ];

    private static readonly (int X, int Y)[] s_offsets =
    [
        (-4, 0),
        (2, 0),
        (1, -2),
        (1, 2),
    ];

    private readonly List<Element> _elements = new();
    private readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };
    private readonly SKPaint _backgroundPaint = new()
    {
        Color = new SKColor(12, 16, 24),
        Style = SKPaintStyle.Fill
    };
    private readonly Random _random = new();
    private GridPoint _lastGridPoint = new(GridWidth / 2, GridHeight / 2);
    private int _complexity = 8;
    private float _cachedScale;
    private float _cachedOffsetX;
    private float _cachedOffsetY;
    private bool _disposed;

    public int ElementCount => _elements.Count;

    public void SetComplexity(int complexity)
    {
        complexity = Math.Clamp(complexity, 0, 24);
        if (_complexity == complexity)
            return;

        _complexity = complexity;
        Resize(ComputeElementCount(_complexity));
    }

    public void Render(SKCanvas canvas, float width, float height)
    {
        Resize(ComputeElementCount(_complexity));

        canvas.DrawRect(new SKRect(0, 0, width, height), _backgroundPaint);

        if (_elements.Count == 0)
            return;

        float scaleX = width / (GridWidth + 1);
        float scaleY = height / (GridHeight + 1);
        float uniformScale = MathF.Min(scaleX, scaleY);
        float offsetX = (width - uniformScale * (GridWidth + 1)) * 0.5f;
        float offsetY = (height - uniformScale * (GridHeight + 1)) * 0.5f;

        EnsurePointCache(uniformScale, offsetX, offsetY);

        Span<Element> elements = CollectionsMarshal.AsSpan(_elements);
        for (int i = 0; i < elements.Length; i++)
        {
            ref Element element = ref elements[i];
            _strokePaint.Color = element.Color;
            _strokePaint.StrokeWidth = element.Width;
            SKPoint[] points = element.Points!;
            for (int point = 1; point < points.Length; point++)
            {
                canvas.DrawLine(points[point - 1], points[point], _strokePaint);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ClearCachedPoints();
        _strokePaint.Dispose();
        _backgroundPaint.Dispose();
        _disposed = true;
    }

    private void Resize(int count)
    {
        int current = _elements.Count;
        if (count == current)
            return;

        if (count < current)
        {
            ClearCachedPoints(count, current);
            _elements.RemoveRange(count, current - count);
            _lastGridPoint = count > 0
                ? _elements[^1].End
                : new GridPoint(GridWidth / 2, GridHeight / 2);
            return;
        }

        _elements.Capacity = Math.Max(_elements.Capacity, count);
        _lastGridPoint = current == 0
            ? new GridPoint(GridWidth / 2, GridHeight / 2)
            : _elements[^1].End;

        for (int i = current; i < count; i++)
        {
            Element element = CreateRandomElement(_lastGridPoint);
            _elements.Add(element);
            _lastGridPoint = element.End;
        }
    }

    private Element CreateRandomElement(GridPoint last)
    {
        int segType = _random.Next(4);
        GridPoint next = RandomPoint(last);

        Element element = default;
        element.Start = last;

        if (segType < 2)
        {
            element.Kind = SegmentKind.Line;
            element.End = next;
        }
        else if (segType == 2)
        {
            GridPoint p2 = RandomPoint(next);
            element.Kind = SegmentKind.Quad;
            element.Control1 = next;
            element.End = p2;
        }
        else
        {
            GridPoint p2 = RandomPoint(next);
            GridPoint p3 = RandomPoint(next);
            element.Kind = SegmentKind.Cubic;
            element.Control1 = next;
            element.Control2 = p2;
            element.End = p3;
        }

        element.Color = s_palette[_random.Next(s_palette.Length)];
        element.Width = (float)(Math.Pow(_random.NextDouble(), 5) * 20.0 + 1.0);
        return element;
    }

    private void EnsurePointCache(float scale, float offsetX, float offsetY)
    {
        bool transformChanged =
            _cachedScale != scale ||
            _cachedOffsetX != offsetX ||
            _cachedOffsetY != offsetY;

        if (transformChanged)
        {
            ClearCachedPoints();
            _cachedScale = scale;
            _cachedOffsetX = offsetX;
            _cachedOffsetY = offsetY;
        }

        Span<Element> elements = CollectionsMarshal.AsSpan(_elements);
        for (int i = 0; i < elements.Length; i++)
        {
            ref Element element = ref elements[i];
            element.Points ??= CreatePoints(in element, scale, offsetX, offsetY);
        }
    }

    private static SKPoint[] CreatePoints(in Element element, float scale, float offsetX, float offsetY)
    {
        SKPoint start = element.Start.ToPoint(scale, offsetX, offsetY);

        switch (element.Kind)
        {
            case SegmentKind.Line:
            {
                return [start, element.End.ToPoint(scale, offsetX, offsetY)];
            }
            case SegmentKind.Quad:
            {
                return CreateQuadPoints(
                    start,
                    element.Control1.ToPoint(scale, offsetX, offsetY),
                    element.End.ToPoint(scale, offsetX, offsetY));
            }
            case SegmentKind.Cubic:
            {
                return CreateCubicPoints(
                    start,
                    element.Control1.ToPoint(scale, offsetX, offsetY),
                    element.Control2.ToPoint(scale, offsetX, offsetY),
                    element.End.ToPoint(scale, offsetX, offsetY));
            }
            default:
            {
                return [start];
            }
        }
    }

    private static SKPoint[] CreateQuadPoints(SKPoint p0, SKPoint p1, SKPoint p2)
    {
        var points = new SKPoint[QuadSegmentCount + 1];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)QuadSegmentCount;
            float u = 1 - t;
            points[i] = new SKPoint(
                u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X,
                u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y);
        }

        return points;
    }

    private static SKPoint[] CreateCubicPoints(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3)
    {
        var points = new SKPoint[CubicSegmentCount + 1];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)CubicSegmentCount;
            float u = 1 - t;
            float uu = u * u;
            float tt = t * t;
            points[i] = new SKPoint(
                uu * u * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + tt * t * p3.X,
                uu * u * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + tt * t * p3.Y);
        }

        return points;
    }

    private void ClearCachedPoints()
    {
        ClearCachedPoints(0, _elements.Count);
    }

    private void ClearCachedPoints(int start, int end)
    {
        Span<Element> elements = CollectionsMarshal.AsSpan(_elements);
        for (int i = start; i < end; i++)
        {
            elements[i].Points = null;
        }
    }

    private static int ComputeElementCount(int complexity)
    {
        if (complexity < 10)
        {
            return (complexity + 1) * 1_000;
        }

        int extended = (complexity - 8) * 10_000;
        return Math.Min(extended, 120_000);
    }

    private GridPoint RandomPoint(GridPoint last)
    {
        var offset = s_offsets[_random.Next(s_offsets.Length)];

        int x = last.X + offset.X;
        if (x < 0 || x > GridWidth)
        {
            x -= offset.X * 2;
        }

        int y = last.Y + offset.Y;
        if (y < 0 || y > GridHeight)
        {
            y -= offset.Y * 2;
        }

        return new GridPoint(x, y);
    }

    private enum SegmentKind : byte
    {
        Line,
        Quad,
        Cubic
    }

    private struct Element
    {
        public SegmentKind Kind;
        public GridPoint Start;
        public GridPoint Control1;
        public GridPoint Control2;
        public GridPoint End;
        public SKColor Color;
        public float Width;
        public SKPoint[]? Points;
    }

    private readonly struct GridPoint
    {
        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public SKPoint ToPoint(float scale, float offsetX, float offsetY)
        {
            float px = offsetX + (X + 0.5f) * scale;
            float py = offsetY + (Y + 0.5f) * scale;
            return new SKPoint(px, py);
        }
    }
}
