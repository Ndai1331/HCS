using System;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace HC.RemoteSigns;

public static class PdfPigSignLocator
{
    public record Hit(int Page, double X, double Y, double Width, double Height);

    public static Hit? Find(byte[] pdfBytes, string marker)
    {
        if (pdfBytes == null || pdfBytes.Length == 0 || string.IsNullOrEmpty(marker))
        {
            return null;
        }

        using var doc = PdfDocument.Open(pdfBytes);

        for (var p = 1; p <= doc.NumberOfPages; p++)
        {
            var page = doc.GetPage(p);
            var letters = page.Letters;

            var text = string.Concat(letters.Select(l => l.Value));
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var segment = letters.Skip(idx).Take(marker.Length).ToList();
            if (segment.Count == 0)
            {
                continue;
            }

            PdfRectangle bbox = segment[0].GlyphRectangle;
            foreach (var lt in segment.Skip(1))
            {
                bbox = Union(bbox, lt.GlyphRectangle);
            }

            return new Hit(
                Page: p,
                X: bbox.Left,
                Y: bbox.Bottom,
                Width: bbox.Width,
                Height: bbox.Height);
        }

        return null;
    }

    private static PdfRectangle Union(PdfRectangle a, PdfRectangle b)
    {
        var left = Math.Min(a.Left, b.Left);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        var right = Math.Max(a.Right, b.Right);
        var top = Math.Max(a.Top, b.Top);
        return new PdfRectangle(left, bottom, right, top);
    }
}
