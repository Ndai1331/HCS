using System;

namespace HC;

public static class PdfFontEnvironment
{
    public const string FontEnvVariableName = "HC_PDF_FONT_ENV";

    public static bool IsProductionFontProfile()
    {
        var explicitEnv = Environment.GetEnvironmentVariable(FontEnvVariableName);
        if (!string.IsNullOrWhiteSpace(explicitEnv))
        {
            if (string.Equals(explicitEnv, "production", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(explicitEnv, "development", StringComparison.OrdinalIgnoreCase)
                || string.Equals(explicitEnv, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(explicitEnv, "dev", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var aspNetCore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(aspNetCore, "Production", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Primary font family for PDFsharp / stamping / placeholders (sans-serif legacy paths).
    /// Linux: DejaVu Sans (full Vietnamese glyphs). Windows/macOS dev: Helvetica.
    /// </summary>
    public static string DefaultPdfFontFamily
    {
        get
        {
            if (OperatingSystem.IsLinux())
            {
                return "DejaVu Sans";
            }

            if (IsProductionFontProfile())
            {
                return "DejaVu Sans";
            }

            return "Helvetica";
        }
    }

    /// <summary>
    /// Serif font for PDF text placeholders when DOCX source is unavailable.
    /// Linux: Liberation Serif (metrically close to Times New Roman). Desktop: Times New Roman.
    /// </summary>
    public static string DefaultPdfSerifFontFamily
    {
        get
        {
            if (OperatingSystem.IsLinux() || IsProductionFontProfile())
            {
                return "Liberation Serif";
            }

            return "Times New Roman";
        }
    }
}
