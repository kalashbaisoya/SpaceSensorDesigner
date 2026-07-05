using System;
using System.Collections.Generic;

namespace SpaceSensorDesigner.Core.Vision;

public sealed class RoomDetectionOptions
{
    /// <summary>Minimum room area as a fraction of the whole image (filters text / furniture symbols).</summary>
    public double MinRoomAreaFraction { get; set; } = 0.004;

    /// <summary>Maximum room area as a fraction of the image (rejects the exterior / whole-image blob).</summary>
    public double MaxRoomAreaFraction { get; set; } = 0.9;

    /// <summary>
    /// Morphological-closing radius (pixels): walls are dilated then eroded by this amount, which
    /// bridges door/window openings so rooms separate from each other and from the exterior, without
    /// permanently thickening the walls. Scale it to about half a door width for the image's resolution.
    /// </summary>
    public int WallSealRadius { get; set; } = 2;
}

/// <summary>An axis-aligned rectangle in image-pixel space (inclusive bounds).</summary>
public readonly record struct PixelRect(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public long Area => (long)Width * Height;
}

/// <summary>
/// Classical-CV room finder for a floor-plan image. Walls are the dark pixels (Otsu threshold);
/// the enclosed light regions between them are rooms. It dilates the walls to seal small gaps,
/// flood-fills the interior into connected components, drops the exterior blob and noise, and
/// returns each remaining region's bounding box. Pure (operates on a grayscale byte buffer) so it
/// is unit-testable without any imaging stack.
/// </summary>
public static class RoomDetector
{
    public static List<PixelRect> Detect(byte[] gray, int width, int height, RoomDetectionOptions? options = null)
    {
        options ??= new RoomDetectionOptions();
        int n = width * height;
        if (n <= 0 || gray.Length < n) return new List<PixelRect>();

        // 1. Threshold: walls = dark pixels. Otsu's split puts {0..t} in the dark class, so use <=
        // (this also handles pure black/white plans, where Otsu returns t = 0).
        int threshold = Otsu(gray, n);
        var wall = new bool[n];
        for (int i = 0; i < n; i++) wall[i] = gray[i] <= threshold;

        // 1b. Keep only the largest connected wall structure. This drops floating text (room labels),
        //     furniture symbols and dimension marks so the dilation below doesn't balloon them into
        //     fake walls that split or swallow rooms.
        wall = KeepLargestWallComponent(wall, width, height);

        // 2. Dilate the walls to bridge door/window openings, so adjacent rooms — and the interior
        //    vs. the exterior — separate cleanly. This shrinks each room by the radius; step 5 grows
        //    the detected boxes back so they still match the true room size.
        int r = options.WallSealRadius;
        for (int p = 0; p < r; p++) wall = Dilate(wall, width, height);

        // 3. Flood-fill the interior (non-wall) into connected components (4-connectivity).
        var label = new int[n]; // 0 = unvisited
        var comps = new List<(long area, int minX, int minY, int maxX, int maxY, bool border)>();
        var stack = new Stack<int>();

        for (int seed = 0; seed < n; seed++)
        {
            if (wall[seed] || label[seed] != 0) continue;

            int id = comps.Count + 1;
            long area = 0;
            int minX = width, minY = height, maxX = 0, maxY = 0;
            bool border = false;

            stack.Push(seed);
            label[seed] = id;
            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                int x = idx % width, y = idx / width;
                area++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1) border = true;

                if (x > 0 && !wall[idx - 1] && label[idx - 1] == 0) { label[idx - 1] = id; stack.Push(idx - 1); }
                if (x < width - 1 && !wall[idx + 1] && label[idx + 1] == 0) { label[idx + 1] = id; stack.Push(idx + 1); }
                if (y > 0 && !wall[idx - width] && label[idx - width] == 0) { label[idx - width] = id; stack.Push(idx - width); }
                if (y < height - 1 && !wall[idx + width] && label[idx + width] == 0) { label[idx + width] = id; stack.Push(idx + width); }
            }
            comps.Add((area, minX, minY, maxX, maxY, border));
        }

        // 4. Keep interior components in a sane size range as rooms. Anything touching the image
        //    border is the exterior (or a sliver against it) — a real plan has a margin around it,
        //    so its rooms never reach the edge.
        long minArea = (long)(options.MinRoomAreaFraction * n);
        long maxArea = (long)(options.MaxRoomAreaFraction * n);
        var rects = new List<PixelRect>();
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c.border) continue;
            if (c.area < minArea || c.area > maxArea) continue;
            // Grow the box back by the dilation radius to recover the true room extent.
            int x0 = Math.Max(0, c.minX - r), y0 = Math.Max(0, c.minY - r);
            int x1 = Math.Min(width - 1, c.maxX + r), y1 = Math.Min(height - 1, c.maxY + r);
            rects.Add(new PixelRect(x0, y0, x1, y1));
        }

        rects.Sort((a, b) => b.Area.CompareTo(a.Area)); // largest first
        return rects;
    }

    /// <summary>Keeps only the biggest 8-connected blob of wall pixels (the building structure).</summary>
    private static bool[] KeepLargestWallComponent(bool[] wall, int width, int height)
    {
        int n = wall.Length;
        var label = new int[n];
        var stack = new Stack<int>();
        int bestId = 0;
        long bestArea = 0;
        int nextId = 0;

        for (int seed = 0; seed < n; seed++)
        {
            if (!wall[seed] || label[seed] != 0) continue;
            int id = ++nextId;
            long area = 0;
            stack.Push(seed);
            label[seed] = id;
            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                area++;
                int x = idx % width, y = idx / width;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int nidx = ny * width + nx;
                    if (wall[nidx] && label[nidx] == 0) { label[nidx] = id; stack.Push(nidx); }
                }
            }
            if (area > bestArea) { bestArea = area; bestId = id; }
        }

        if (bestId == 0) return wall;
        var kept = new bool[n];
        for (int i = 0; i < n; i++) kept[i] = label[i] == bestId;
        return kept;
    }

    /// <summary>Otsu's method: the grayscale threshold that best separates dark (wall) from light (floor).</summary>
    private static int Otsu(byte[] gray, int n)
    {
        Span<int> hist = stackalloc int[256];
        for (int i = 0; i < n; i++) hist[gray[i]]++;

        double sum = 0;
        for (int i = 0; i < 256; i++) sum += i * (double)hist[i];

        double sumB = 0;
        long wB = 0;
        double maxBetween = 0;
        int threshold = 127;
        for (int i = 0; i < 256; i++)
        {
            wB += hist[i];
            if (wB == 0) continue;
            long wF = n - wB;
            if (wF == 0) break;
            sumB += i * (double)hist[i];
            double mB = sumB / wB;
            double mF = (sum - sumB) / wF;
            double between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > maxBetween) { maxBetween = between; threshold = i; }
        }
        return threshold;
    }

    /// <summary>One pass of 4-connected binary dilation (out-of-bounds treated as background).</summary>
    private static bool[] Dilate(bool[] src, int width, int height)
    {
        var dst = new bool[src.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                dst[i] = src[i]
                    || (x > 0 && src[i - 1])
                    || (x < width - 1 && src[i + 1])
                    || (y > 0 && src[i - width])
                    || (y < height - 1 && src[i + width]);
            }
        }
        return dst;
    }
}
