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
/// Overlays a small "Đã ký" badge at the bottom-right corner of the handwritten signature image.
/// Layout asset: Assets/Signing/electronic-signature-layout.png (embedded resource).
/// </summary>
public static class ElectronicSignatureLayoutComposer
{
    private const string LayoutResourceSuffix = "electronic-signature-layout.png";

    /// <summary>Badge width relative to signature width (keeps "Đã ký" compact).</summary>
    private const double LayoutBadgeWidthRatio = 0.28;

    private const int BadgeMarginPx = 6;

    // Upscale final composite so Word/LibreOffice renders a readable image at ~4cm width.
    private const int ExportMinWidthPx = 504;

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
            using var layoutBadge = Image.Load<Rgba32>(layoutBytes);
            using var signature = Image.Load<Rgba32>(signatureImageBytes);
            CropSignatureBorder(signature);

            var badgeWidth = Math.Clamp(
                (int)Math.Round(signature.Width * LayoutBadgeWidthRatio),
                48,
                Math.Max(48, signature.Width - BadgeMarginPx * 2));
            var badgeHeight = Math.Max(
                1,
                (int)Math.Round(layoutBadge.Height * (badgeWidth / (double)layoutBadge.Width)));

            layoutBadge.Mutate(ctx => ctx.Resize(badgeWidth, badgeHeight));

            var posX = Math.Max(0, signature.Width - layoutBadge.Width - BadgeMarginPx);
            var posY = Math.Max(0, signature.Height - layoutBadge.Height - BadgeMarginPx);

            signature.Mutate(ctx => ctx.DrawImage(layoutBadge, new Point(posX, posY), 1f));

            if (signature.Width < ExportMinWidthPx)
            {
                var exportHeight = Math.Max(
                    1,
                    (int)Math.Round(signature.Height * ((double)ExportMinWidthPx / signature.Width)));
                signature.Mutate(ctx => ctx.Resize(ExportMinWidthPx, exportHeight));
            }

            using var output = new MemoryStream();
            signature.Save(output, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
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
