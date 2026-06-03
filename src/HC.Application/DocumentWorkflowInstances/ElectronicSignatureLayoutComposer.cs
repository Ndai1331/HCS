using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Composites a handwritten signature under the "Đã ký" layout banner (layout on top).
/// Layout asset: Assets/Signing/electronic-signature-layout.png (embedded resource).
/// </summary>
public static class ElectronicSignatureLayoutComposer
{
    private const string LayoutResourceSuffix = "electronic-signature-layout.png";

    // Left portion of the banner reserved for the signature image (layout is 168x69).
    private const double SignatureZoneWidthRatio = 0.58;
    private const int ZonePadding = 4;
    // Export at 3x native size so Word/LibreOffice renders a readable banner at ~4cm width.
    private const int ExportLayoutWidthPx = 504;

    private static byte[]? _cachedLayoutBytes;

    public static byte[] Compose(byte[] signatureImageBytes)
    {
        if (signatureImageBytes is not { Length: > 0 })
        {
            return signatureImageBytes;
        }

        try
        {
            var layoutBytes = GetLayoutBytes();
            using var layout = Image.Load<Rgba32>(layoutBytes);
            using var signature = Image.Load<Rgba32>(signatureImageBytes);
            CropSignatureBorder(signature);

            var zoneWidth = Math.Max(1, (int)(layout.Width * SignatureZoneWidthRatio) - ZonePadding * 2);
            var zoneHeight = Math.Max(1, layout.Height - ZonePadding * 2);

            signature.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(zoneWidth, zoneHeight),
                Mode = ResizeMode.Max
            }));

            var posX = ZonePadding + Math.Max(0, (zoneWidth - signature.Width) / 2);
            var posY = ZonePadding + Math.Max(0, (zoneHeight - signature.Height) / 2);

            // Signature first (bottom), layout overlay on top so "Đã ký" is never covered.
            using var composite = new Image<Rgba32>(layout.Width, layout.Height);
            composite.Mutate(ctx => ctx.DrawImage(signature, new Point(posX, posY), 1f));
            composite.Mutate(ctx => ctx.DrawImage(layout, Point.Empty, 1f));

            if (composite.Width < ExportLayoutWidthPx)
            {
                var exportHeight = Math.Max(1, (int)Math.Round(composite.Height * ((double)ExportLayoutWidthPx / composite.Width)));
                composite.Mutate(ctx => ctx.Resize(ExportLayoutWidthPx, exportHeight));
            }

            using var output = new MemoryStream();
            composite.Save(output, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
            return output.ToArray();
        }
        catch
        {
            // Keep signing flow working if layout asset is missing or image decode fails.
            return signatureImageBytes;
        }
    }

    private static void CropSignatureBorder(Image<Rgba32> signature)
    {
        // Some uploaded signatures contain a thin rectangular frame at image edges.
        // Crop a tiny outer margin to remove that frame before compositing.
        const int borderCropPx = 2;
        if (signature.Width <= borderCropPx * 2 || signature.Height <= borderCropPx * 2)
        {
            return;
        }

        signature.Mutate(ctx => ctx.Crop(new Rectangle(
            borderCropPx,
            borderCropPx,
            signature.Width - borderCropPx * 2,
            signature.Height - borderCropPx * 2)));
    }

    private static byte[] GetLayoutBytes()
    {
        if (_cachedLayoutBytes != null)
        {
            return _cachedLayoutBytes;
        }

        var assembly = typeof(ElectronicSignatureLayoutComposer).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith(LayoutResourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new FileNotFoundException(
                $"Electronic signature layout resource '{LayoutResourceSuffix}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Cannot open embedded resource '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        _cachedLayoutBytes = memory.ToArray();
        return _cachedLayoutBytes;
    }
}
