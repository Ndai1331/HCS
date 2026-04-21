using System;

namespace HC;

/// <summary>
/// Chooses the default PDF/signature font family for local dev vs production.
/// Production: Liberation Sans (typical on Linux servers). Dev/local: Helvetica (macOS-friendly).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Set <c>HC_PDF_FONT_ENV</c> to <c>production</c> or <c>development</c> (aliases: <c>local</c>, <c>dev</c>).</description></item>
/// <item><description>If unset, uses <c>ASPNETCORE_ENVIRONMENT=Production</c> as production; any other value is treated as non-production.</description></item>
/// </list>
/// </remarks>
public static class PdfFontEnvironment
{
    /// <summary>Environment variable name override for font profile.</summary>
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

    /// <summary>Primary font family for PDFsharp / stamping / placeholders.</summary>
    public static string DefaultPdfFontFamily => IsProductionFontProfile() ? "Liberation Sans" : "Helvetica";
}
