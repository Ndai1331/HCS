using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using PdfSharp.Fonts;

namespace HC.Helpers;

/// <summary>
/// Custom font resolver for PDFsharp 6.x on non-Windows platforms (macOS, Linux).
/// Searches system font directories and maps common font families (Helvetica, Arial, etc.)
/// to available TTF/TTC files.
///
/// Usage: GlobalFontSettings.FontResolver = new CustomFontResolver();
/// </summary>
public class CustomFontResolver : IFontResolver
{
    // Cache: faceName -> font bytes (avoid re-reading from disk)
    private static readonly ConcurrentDictionary<string, byte[]> _fontCache = new();

    // Cache: resolved font file paths (familyName_bold_italic -> filePath)
    private static readonly ConcurrentDictionary<string, string?> _pathCache = new();

    // System font directories per platform
    private static readonly string[] _fontDirectories = GetFontDirectories();

    /// <summary>
    /// Resolve a typeface to a font file.
    /// PDFsharp calls this to find the font matching a family name + style.
    /// </summary>
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Normalize family name for lookup
        var normalizedFamily = NormalizeFamilyName(familyName);

        // Build a unique face name that encodes family + style
        var faceName = BuildFaceName(normalizedFamily, isBold, isItalic);

        // Try to find the font file path
        var fontPath = FindFontPath(normalizedFamily, isBold, isItalic);
        if (fontPath == null)
        {
            // Fallback: try without style modifiers (use regular, simulate bold/italic)
            fontPath = FindFontPath(normalizedFamily, false, false);
            if (fontPath != null)
            {
                faceName = BuildFaceName(normalizedFamily, false, false);
                return new FontResolverInfo(faceName, mustSimulateBold: isBold, mustSimulateItalic: isItalic);
            }

            return null;
        }

        return new FontResolverInfo(faceName);
    }

    /// <summary>
    /// Get the font bytes for a previously resolved face name.
    /// PDFsharp calls this after ResolveTypeface returns a FontResolverInfo.
    /// </summary>
    public byte[]? GetFont(string faceName)
    {
        if (_fontCache.TryGetValue(faceName, out var cached))
            return cached;

        // Parse faceName back to family + style to find path
        ParseFaceName(faceName, out var family, out var bold, out var italic);
        var fontPath = FindFontPath(family, bold, italic);

        if (fontPath == null)
            return null;

        var bytes = File.ReadAllBytes(fontPath);
        _fontCache.TryAdd(faceName, bytes);
        return bytes;
    }

    #region Font Path Resolution

    /// <summary>
    /// Map common font family names to actual font file names to search for.
    /// Helvetica -> Arial (they are metrically very similar).
    /// </summary>
    private static string NormalizeFamilyName(string familyName)
    {
        return familyName.Trim().ToLowerInvariant() switch
        {
            "helvetica" => "arial",
            "helvetica neue" => "arial",
            "times" => "times new roman",
            "times-roman" => "times new roman",
            "courier" => "courier new",
            _ => familyName.Trim().ToLowerInvariant()
        };
    }

    /// <summary>
    /// Find the font file path on disk for a given family name + style.
    /// Searches through system font directories.
    /// </summary>
    private static string? FindFontPath(string normalizedFamily, bool isBold, bool isItalic)
    {
        var cacheKey = $"{normalizedFamily}_{isBold}_{isItalic}";
        if (_pathCache.TryGetValue(cacheKey, out var cachedPath))
            return cachedPath;

        // Build candidate file names to search for
        var candidates = GetCandidateFileNames(normalizedFamily, isBold, isItalic);

        foreach (var dir in _fontDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (var candidate in candidates)
            {
                // Search in the directory (case-insensitive on macOS/Linux)
                var fullPath = Path.Combine(dir, candidate);
                if (File.Exists(fullPath))
                {
                    _pathCache.TryAdd(cacheKey, fullPath);
                    return fullPath;
                }

                // Also try subdirectories (Linux fonts are often in subdirs like truetype/dejavu/)
                try
                {
                    var found = Directory.GetFiles(dir, candidate, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (found != null)
                    {
                        _pathCache.TryAdd(cacheKey, found);
                        return found;
                    }
                }
                catch
                {
                    // Permission denied or other IO error - skip this directory
                }
            }
        }

        // Fallback: fuzzy-match by filename tokens (handles distro-specific naming).
        foreach (var dir in _fontDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            try
            {
                var allFontFiles = Directory
                    .EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var wantedTokens = normalizedFamily
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.ToLowerInvariant())
                    .ToArray();

                var styleToken = isBold && isItalic
                    ? new[] { "bold", "italic", "oblique", "bi" }
                    : isBold
                        ? new[] { "bold", "bd" }
                        : isItalic
                            ? new[] { "italic", "oblique", "it" }
                            : new[] { "regular", "book", "roman" };

                var fuzzyMatch = allFontFiles.FirstOrDefault(path =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    var hasFamily = wantedTokens.All(fileName.Contains);
                    if (!hasFamily)
                    {
                        return false;
                    }

                    // For regular style, family match is enough.
                    if (!isBold && !isItalic)
                    {
                        return true;
                    }

                    return styleToken.Any(fileName.Contains);
                });

                if (fuzzyMatch != null)
                {
                    _pathCache.TryAdd(cacheKey, fuzzyMatch);
                    return fuzzyMatch;
                }
            }
            catch
            {
                // Ignore IO/permission errors and continue.
            }
        }

        // Helvetica maps to Arial; Arial is often absent on Linux (proprietary). Use common free substitutes.
        if (normalizedFamily == "arial")
        {
            var substitute = FindFontPath("dejavu sans", isBold, isItalic)
                ?? FindFontPath("liberation sans", isBold, isItalic)
                ?? FindFontPath("noto sans", isBold, isItalic);
            if (substitute != null)
            {
                _pathCache.TryAdd(cacheKey, substitute);
                return substitute;
            }
        }

        // Last resort: try any font as ultimate fallback
        if (normalizedFamily != "arial" && normalizedFamily != "dejavu sans" && normalizedFamily != "liberation sans"
            && normalizedFamily != "noto sans")
        {
            // Try Arial, then DejaVu Sans, then Liberation Sans as fallbacks
            var fallback = FindFontPath("arial", isBold, isItalic)
                ?? FindFontPath("dejavu sans", isBold, isItalic)
                ?? FindFontPath("liberation sans", isBold, isItalic)
                ?? FindFontPath("noto sans", isBold, isItalic);
            _pathCache.TryAdd(cacheKey, fallback);
            return fallback;
        }

        _pathCache.TryAdd(cacheKey, null);
        return null;
    }

    /// <summary>
    /// Get candidate file names for a font family + style combination.
    /// Returns possible file names to search for in font directories.
    /// </summary>
    private static string[] GetCandidateFileNames(string normalizedFamily, bool isBold, bool isItalic)
    {
        // Build style suffix variations
        var styleVariations = (isBold, isItalic) switch
        {
            (true, true) => new[] { " Bold Italic", "-BoldItalic", "bi", "-BoldOblique", " Bold Oblique" },
            (true, false) => new[] { " Bold", "-Bold", "bd", "b" },
            (false, true) => new[] { " Italic", "-Italic", "i", "-Oblique", " Oblique" },
            _ => new[] { "", "-Regular", " Regular" }
        };

        // Map normalized family to known file name patterns
        var fileBaseName = normalizedFamily switch
        {
            "arial" => "Arial",
            "times new roman" => "Times New Roman",
            "courier new" => "Courier New",
            "dejavu sans" => "DejaVuSans",
            "liberation sans" => "LiberationSans",
            "noto sans" => "NotoSans",
            _ => normalizedFamily
        };

        // Generate all candidate names with both .ttf and .ttc extensions
        var candidates = styleVariations
            .SelectMany(style => new[]
            {
                $"{fileBaseName}{style}.ttf",
                $"{fileBaseName}{style}.ttc",
                $"{fileBaseName}{style}.otf"
            })
            .ToList();

        // Add Arial Unicode as a fallback (great Unicode/Vietnamese support)
        if (normalizedFamily == "arial" && !isBold && !isItalic)
        {
            candidates.Add("Arial Unicode.ttf");
            candidates.Add("ArialUnicode.ttf");
        }

        return candidates.ToArray();
    }

    #endregion

    #region Face Name Encoding

    private static string BuildFaceName(string normalizedFamily, bool isBold, bool isItalic)
    {
        var style = (isBold, isItalic) switch
        {
            (true, true) => "_BI",
            (true, false) => "_B",
            (false, true) => "_I",
            _ => "_R"
        };
        return $"{normalizedFamily}{style}";
    }

    private static void ParseFaceName(string faceName, out string family, out bool bold, out bool italic)
    {
        bold = false;
        italic = false;

        if (faceName.EndsWith("_BI"))
        {
            family = faceName[..^3];
            bold = true;
            italic = true;
        }
        else if (faceName.EndsWith("_B"))
        {
            family = faceName[..^2];
            bold = true;
        }
        else if (faceName.EndsWith("_I"))
        {
            family = faceName[..^2];
            italic = true;
        }
        else if (faceName.EndsWith("_R"))
        {
            family = faceName[..^2];
        }
        else
        {
            family = faceName;
        }
    }

    #endregion

    #region Platform Detection

    private static string[] GetFontDirectories()
    {
        // Allow injecting font directories via env var (semicolon-separated).
        // Example: HC_FONT_DIRS="/app/fonts;/usr/share/fonts"
        var envDirs = (Environment.GetEnvironmentVariable("HC_FONT_DIRS") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Always include app-local fonts folder for container/distroless environments.
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appFonts = new[]
        {
            Path.Combine(baseDir, "fonts"),
            "/app/fonts"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return envDirs
                .Concat(appFonts)
                .Concat(new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)),
                @"C:\Windows\Fonts"
            })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return envDirs
                .Concat(appFonts)
                .Concat(new[]
            {
                "/System/Library/Fonts/Supplemental",
                "/System/Library/Fonts",
                "/Library/Fonts",
                Path.Combine(home, "Library/Fonts")
            })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // Linux
        return envDirs
            .Concat(appFonts)
            .Concat(new[]
            {
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                "/usr/share/fonts/truetype",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts")
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    #endregion
}
