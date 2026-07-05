using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;

namespace SpaceSensorDesigner.App.Services;

/// <summary>
/// Renders a page of a PDF to a bitmap so it can be used as a traceable floor-plan background,
/// exactly like an imported PNG/JPG. Uses Docnet.Core (bundled PDFium) — no external tools.
/// </summary>
public static class PdfImportService
{
    private const double TargetDpi = 150.0;   // crisp enough to trace walls
    private const double PdfDpi = 72.0;        // PDF user-space is 1/72"
    private const int MaxPixels = 3000;        // cap the long edge so huge pages stay sane

    public static int GetPageCount(string pdfPath)
    {
        var bytes = File.ReadAllBytes(pdfPath);
        using var doc = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0));
        return doc.GetPageCount();
    }

    /// <summary>Renders one page (0-based) to a frozen white-backed <see cref="BitmapSource"/>.</summary>
    public static BitmapSource RenderPage(string pdfPath, int pageIndex = 0)
    {
        var bytes = File.ReadAllBytes(pdfPath);
        double scale = TargetDpi / PdfDpi;

        // Probe the native page size so we can cap very large pages.
        using (var probe = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.0)))
        {
            if (pageIndex < 0 || pageIndex >= probe.GetPageCount())
                throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page is out of range.");
            using var pp = probe.GetPageReader(pageIndex);
            double maxNative = Math.Max(pp.GetPageWidth(), pp.GetPageHeight());
            if (maxNative > 0 && maxNative * scale > MaxPixels) scale = MaxPixels / maxNative;
            if (scale < 1.0) scale = 1.0;
        }

        using var doc = DocLib.Instance.GetDocReader(bytes, new PageDimensions(scale));
        using var page = doc.GetPageReader(pageIndex);
        int w = page.GetPageWidth();
        int h = page.GetPageHeight();
        byte[] raw = page.GetImage(); // BGRA, w*h*4

        FlattenOntoWhite(raw); // PDFium leaves empty areas transparent; a floor plan should read as white

        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, raw, w * 4);
        bmp.Freeze();
        return bmp;
    }

    public static void SavePng(BitmapSource bmp, string path)
    {
        using var fs = File.Create(path);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        enc.Save(fs);
    }

    private static void FlattenOntoWhite(byte[] bgra)
    {
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            byte a = bgra[i + 3];
            if (a == 255) continue;
            int inv = 255 - a;
            bgra[i] = (byte)((bgra[i] * a + 255 * inv) / 255);       // B
            bgra[i + 1] = (byte)((bgra[i + 1] * a + 255 * inv) / 255); // G
            bgra[i + 2] = (byte)((bgra[i + 2] * a + 255 * inv) / 255); // R
            bgra[i + 3] = 255;
        }
    }
}
