using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpaceSensorDesigner.Core.Models;
using SpaceSensorDesigner.Core.Vision;

namespace SpaceSensorDesigner.App.Services;

/// <summary>
/// Turns the traced-over floor-plan background into <see cref="Room"/> polygons: it reads the image
/// pixels (downscaled for speed), runs the <see cref="RoomDetector"/>, then maps the detected pixel
/// rectangles back into world metres using the plan's background origin + scale.
/// </summary>
public static class RoomDetectionService
{
    private const int MaxLongEdge = 1200; // downscale big images so detection stays fast

    public static List<Room> DetectRooms(BitmapSource image, Vec2 origin, double metresPerPixel)
    {
        // Downscale to keep the flood-fill cheap.
        double longEdge = System.Math.Max(image.PixelWidth, image.PixelHeight);
        double scale = longEdge > MaxLongEdge ? MaxLongEdge / longEdge : 1.0;
        BitmapSource src = scale < 1.0
            ? new TransformedBitmap(image, new ScaleTransform(scale, scale))
            : image;

        var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        int stride = w * 4;
        var px = new byte[h * stride];
        bgra.CopyPixels(px, stride, 0);

        var gray = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int j = i * 4; // BGRA
            gray[i] = (byte)((px[j] * 114 + px[j + 1] * 587 + px[j + 2] * 299) / 1000);
        }

        // Seal gaps up to ~half a door width (0.45 m). One detected pixel spans metresPerPixel / scale
        // metres, so convert that world distance into a pixel radius (clamped to a sane range).
        double metresPerDetectedPixel = metresPerPixel / scale;
        int sealRadius = metresPerDetectedPixel > 0
            ? (int)System.Math.Round(0.45 / metresPerDetectedPixel)
            : 2;
        sealRadius = System.Math.Clamp(sealRadius, 2, 40);

        var rects = RoomDetector.Detect(gray, w, h, new RoomDetectionOptions { WallSealRadius = sealRadius });

        // Map detected (downscaled) pixels back to original pixels, then to world metres.
        double invScale = 1.0 / scale;
        var rooms = new List<Room>(rects.Count);
        int n = 1;
        foreach (var r in rects)
        {
            double x0 = origin.X + r.MinX * invScale * metresPerPixel;
            double y0 = origin.Y + r.MinY * invScale * metresPerPixel;
            double x1 = origin.X + (r.MaxX + 1) * invScale * metresPerPixel;
            double y1 = origin.Y + (r.MaxY + 1) * invScale * metresPerPixel;

            rooms.Add(new Room
            {
                Name = $"Room {n++}",
                Type = RoomType.Other,
                Polygon = { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) }
            });
        }
        return rooms;
    }
}
