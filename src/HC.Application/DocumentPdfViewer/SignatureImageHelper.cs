using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HC.DocumentPdfViewer;

/// <summary>
/// PdfSharp on Linux renders PNG alpha channels as black.
/// This helper composites PNG images onto a white background,
/// producing an opaque RGB PNG that renders correctly everywhere.
/// </summary>
public static class SignatureImageHelper
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47 };

    public static byte[] FlattenTransparency(byte[] imageBytes)
    {
        if (imageBytes is not { Length: > 4 })
            return imageBytes;

        if (!IsPng(imageBytes))
            return imageBytes;

        try
        {
            using var original = Image.Load<Rgba32>(imageBytes);
            using var flattened = new Image<Rgba32>(original.Width, original.Height, Color.White);
            flattened.Mutate(ctx => ctx.DrawImage(original, 1f));

            using var output = new MemoryStream();
            flattened.Save(output, new PngEncoder { ColorType = PngColorType.Rgb });
            return output.ToArray();
        }
        catch
        {
            return imageBytes;
        }
    }

    private static bool IsPng(byte[] data)
    {
        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (data[i] != PngSignature[i]) return false;
        }
        return true;
    }
}
