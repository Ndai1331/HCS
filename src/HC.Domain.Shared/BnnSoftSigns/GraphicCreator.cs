using Bnn.SignLib;
using HC;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace HC.BnnSoftSigns
{
    internal static class SignGraphicRuntimeContext
    {
        private static readonly AsyncLocal<byte[]?> LayoutOverride = new();

        public static byte[]? CurrentLayout => LayoutOverride.Value;

        public static IDisposable UseLayout(byte[]? layoutBytes)
        {
            var previous = LayoutOverride.Value;
            LayoutOverride.Value = layoutBytes is { Length: > 0 } ? layoutBytes : null;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly byte[]? _previous;
            private bool _disposed;

            public Scope(byte[]? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                LayoutOverride.Value = _previous;
                _disposed = true;
            }
        }
    }

    public class GraphicCreator : IGraphicCreator
    {
        private static readonly Assembly Assembly = typeof(GraphicCreator).Assembly;

        static Stream ToStream(byte[] bytes)
        {
            return new MemoryStream(bytes)
            {
                Position = 0
            };
        }

        private static string GetAvailableSystemFontFamilyName()
        {
            // Linux/production: DejaVu Sans; dev desktop: Helvetica (see PdfFontEnvironment).
            var primary = PdfFontEnvironment.DefaultPdfFontFamily;
            var preferred = new List<string> { primary };
            foreach (var name in new[] { "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans", "Noto Sans", "FreeSans" })
            {
                if (!preferred.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    preferred.Add(name);
                }
            }

            foreach (var name in preferred)
            {
                if (SystemFonts.Collection.TryGet(name, out var _))
                {
                    return name;
                }
            }

            var any = SystemFonts.Collection.Families.FirstOrDefault();
            return any.Name ?? "Arial";
        }

        private static byte[] LoadLayoutTemplate(string fileName)
        {
            // 1) Prefer embedded resource so runtime does not depend on current directory.
            var embeddedName = Assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x =>
                    x.EndsWith($".BnnSoftSigns.{fileName}", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(embeddedName))
            {
                using var resourceStream = Assembly.GetManifestResourceStream(embeddedName);
                if (resourceStream != null)
                {
                    using var memory = new MemoryStream();
                    resourceStream.CopyTo(memory);
                    return memory.ToArray();
                }
            }

            // 2) Backward-compatible file system fallback for existing deployments.
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "image", fileName),
                Path.Combine(AppContext.BaseDirectory, "BnnSoftSigns", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "image", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "BnnSoftSigns", fileName)
            };

            var existingPath = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(existingPath))
            {
                return File.ReadAllBytes(existingPath);
            }

            throw new FileNotFoundException(
                $"Cannot load signing layout template '{fileName}'. " +
                $"Tried embedded resources and runtime folders: {string.Join(" | ", candidates)}");
        }

        public byte[] CreateGraphic(string nguoiky, string chucvu, string ngayky, byte[] chuky, byte[] condau, int width = 433, int height = 250, int x1 = 10, int x2 = 140, int linebreak = 38)
        {
            byte[]? layout = null;
            var hasSeal = condau != null && condau.Length > 0;
            var hasSignature = chuky != null && chuky.Length > 0;

            var xcondau = 2;
            var ycondau = 20;
            var xchuky = 0;
            var ychuky = 0;
            var ynguoiky = 0;
            var xnguoiky = 0;
            var daichuky = 160;
            var caochuky = 100;

            var runtimeLayout = SignGraphicRuntimeContext.CurrentLayout;
            if (runtimeLayout is { Length: > 0 })
            {
                try
                {
                    Console.Error.WriteLine($"[GRAPHIC_LAYOUT] Using runtime layout bytes | Length={runtimeLayout.Length}");
                }
                catch
                {
                    // Ignore logging failures in graphic generation path.
                }
                layout = runtimeLayout;
                width = 433;
                height = 250;
                linebreak = 38;
            }
            else if (hasSeal)
            {
                try
                {
                    Console.Error.WriteLine("[GRAPHIC_LAYOUT] Runtime layout missing. Fallback to embedded/file layout.png");
                }
                catch
                {
                    // Ignore logging failures in graphic generation path.
                }
                layout = LoadLayoutTemplate("layout.png");
                width = 433;
                height = 250;
                linebreak = 38;
            }
            else
            {
                try
                {
                    Console.Error.WriteLine("[GRAPHIC_LAYOUT] Runtime layout missing and no seal. Fallback to embedded/file layout2.png");
                }
                catch
                {
                    // Ignore logging failures in graphic generation path.
                }
                layout = LoadLayoutTemplate("layout2.png");
                width = 433;
                height = 250;
                linebreak = 38;
            }

            // Try SkiaSharp first; fall back to ImageSharp if it fails
            try
            {
                var info = new SKImageInfo(width, height);
                using (var surface = SKSurface.Create(info))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);

                    // load the embedded resource stream
                    using (var stream = new SKManagedStream(new MemoryStream(layout)))
                    using (var codec = SKCodec.Create(stream))
                    using (var bitmap = new SKBitmap(info.Width, info.Height, info.ColorType, info.IsOpaque ? SKAlphaType.Opaque : SKAlphaType.Premul))
                    {
                        if (codec == null)
                        {
                            throw new InvalidDataException("Cannot decode layout image bytes.");
                        }

                        try
                        {
                            Console.Error.WriteLine(
                                $"[GRAPHIC_LAYOUT] Decoded layout metadata | EncodedWidth={codec.Info.Width} | EncodedHeight={codec.Info.Height} | ColorType={codec.Info.ColorType}");
                        }
                        catch
                        {
                            // Ignore logging failures in graphic generation path.
                        }

                        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
                        try
                        {
                            Console.Error.WriteLine(
                                $"[GRAPHIC_LAYOUT] Layout rasterization result | CodecResult={result} | TargetWidth={bitmap.Width} | TargetHeight={bitmap.Height}");
                        }
                        catch
                        {
                            // Ignore logging failures in graphic generation path.
                        }

                        if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                        {
                            canvas.DrawBitmap(bitmap, 0, 0);
                        }
                        else
                        {
                            // Some images return InvalidScale when target size differs from encoded size.
                            using var fallbackBitmap = SKBitmap.Decode(layout);
                            if (fallbackBitmap == null)
                            {
                                throw new InvalidDataException($"Cannot decode layout image. CodecResult={result}");
                            }

                            try
                            {
                                Console.Error.WriteLine(
                                    $"[GRAPHIC_LAYOUT] Fallback decode used | SourceWidth={fallbackBitmap.Width} | SourceHeight={fallbackBitmap.Height} | DrawWidth={width} | DrawHeight={height}");
                            }
                            catch
                            {
                                // Ignore logging failures in graphic generation path.
                            }

                            var destination = new SKRect(0, 0, width, height);
                            canvas.DrawBitmap(fallbackBitmap, destination);
                        }
                    }

                    // draw condau and signature images if provided
                    if (hasSeal)
                    {
                        using (var magstream = ToStream(condau!))
                        using (var stream = new SKManagedStream(magstream))
                        using (var bitmap = SKBitmap.Decode(stream))
                        using (var paint2 = new SKPaint())
                        {
                            if (bitmap == null)
                            {
                                throw new InvalidDataException("Invalid seal image bytes.");
                            }

                            var bmpRect = SKRectI.Create(200, 200).AspectFit(bitmap.Info.Size);
                            using (var resized = new SKBitmap(bmpRect.Width, bmpRect.Height))
                            {
                                bitmap.ScalePixels(resized, SKFilterQuality.High);
                                paint2.StrokeWidth = 3;
                                paint2.IsStroke = true;
                                paint2.Color = SKColor.Parse("0d0d0d");
                                paint2.Style = SKPaintStyle.Stroke;
                                canvas.DrawBitmap(resized, xcondau, ycondau, paint2);
                                xchuky = xcondau + resized.Width;
                                ychuky = ycondau;
                            }
                        }
                    }
                    else
                    {
                        xchuky = 50;
                        ychuky = 20;
                    }

                    if (hasSignature)
                    {
                        using (var magstream = ToStream(chuky!))
                        using (var stream = new SKManagedStream(magstream))
                        using (var bitmap = SKBitmap.Decode(stream))
                        using (var paint2 = new SKPaint())
                        {
                            if (bitmap == null)
                            {
                                throw new InvalidDataException("Invalid signature image bytes.");
                            }

                            var bmpRect = SKRectI.Create(daichuky, caochuky).AspectFit(bitmap.Info.Size);
                            using (var resized = new SKBitmap(bmpRect.Width, bmpRect.Height))
                            {
                                bitmap.ScalePixels(resized, SKFilterQuality.High);
                                paint2.StrokeWidth = 3;
                                paint2.IsStroke = true;
                                paint2.Color = SKColor.Parse("0d0d0d");
                                paint2.Style = SKPaintStyle.Stroke;
                                canvas.DrawBitmap(resized, xchuky, ychuky, paint2);
                                xnguoiky = xchuky - 50;
                                ynguoiky = ychuky + resized.Height + 20;
                            }
                        }
                    }

                    using (var paintKey = new SKPaint())
                    using (var paintValue = new SKPaint())
                    {
                        paintKey.IsAntialias = true;
                        paintKey.TextSize = 20;
                        paintKey.Typeface = SKTypeface.FromFamilyName("Arial");
                        paintKey.Color = SKColors.Black;
                        paintKey.Style = SKPaintStyle.Fill;
                        paintKey.TextAlign = SKTextAlign.Left;

                        paintValue.IsAntialias = true;
                        paintValue.TextSize = 20;
                        paintValue.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
                        paintValue.Color = SKColors.Black;
                        paintValue.Style = SKPaintStyle.Fill;
                        paintValue.TextAlign = SKTextAlign.Left;

                        if (!string.IsNullOrEmpty(nguoiky))
                        {
                            float textlen1 = paintValue.MeasureText(nguoiky);
                            float textlen2 = paintKey.MeasureText("Ngày ký:") + paintValue.MeasureText(ngayky) + 30;
                            float textlen = textlen2 > textlen1 ? textlen2 : textlen1;
                            while (textlen > width)
                            {
                                paintValue.TextSize = paintValue.TextSize - 1;
                                paintKey.TextSize = paintValue.TextSize;
                                textlen1 = paintValue.MeasureText(nguoiky);
                                textlen2 = paintKey.MeasureText("Ngày ký:") + paintValue.MeasureText(ngayky) + 30;
                                textlen = textlen2 > textlen1 ? textlen2 : textlen1;
                            }
                            xnguoiky = (int)(width - textlen);

                            canvas.DrawText(nguoiky, new SKPoint(xnguoiky, ynguoiky), paintValue);
                            ynguoiky += linebreak;
                        }

                        canvas.DrawText("Ngày ký:", new SKPoint(xnguoiky, ynguoiky), paintKey);
                        canvas.DrawText(ngayky, new SKPoint(xnguoiky + 100, ynguoiky), paintValue);
                    }

                    using (var image = surface.Snapshot())
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        return data.ToArray();
                    }
                }
            }
            catch (System.Exception skEx)
            {
                // Log Skia exception then fallback to ImageSharp
                try { System.Console.Error.WriteLine("Skia failed: " + skEx.ToString()); } catch { }

                // Fallback implementation using ImageSharp
                using (var img = Image.Load<Rgba32>(layout))
                {
                    img.Mutate(x => x.Resize(width, height));

                    if (hasSeal)
                    {
                        using var condauImg = Image.Load<Rgba32>(condau);
                        condauImg.Mutate(y => y.Resize(200, 200));
                        img.Mutate(ctx => ctx.DrawImage(condauImg, new Point(xcondau, ycondau), 1f));
                        xchuky = xcondau + 200;
                        ychuky = ycondau;
                    }

                    if (hasSignature)
                    {
                        using var chukyImg = Image.Load<Rgba32>(chuky);
                        chukyImg.Mutate(y => y.Resize(daichuky, caochuky));
                        img.Mutate(ctx => ctx.DrawImage(chukyImg, new Point(xchuky, ychuky), 1f));
                        xnguoiky = xchuky - 50;
                        ynguoiky = ychuky + daichuky + 20;
                    }

                    var fontFamilyName = GetAvailableSystemFontFamilyName();
                    var font = SystemFonts.CreateFont(fontFamilyName, 20);

                    img.Mutate(ctx => ctx.DrawText(nguoiky ?? string.Empty, font, SixLabors.ImageSharp.Color.Black, new PointF(xnguoiky, ynguoiky)));
                    img.Mutate(ctx => ctx.DrawText("Ngày ký:", font, SixLabors.ImageSharp.Color.Black, new PointF(xnguoiky, ynguoiky + linebreak)));

                    using var ms = new MemoryStream();
                    img.SaveAsPng(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
