using System;
using System.IO;
using System.Text.RegularExpressions;

namespace HC.BlobStoring;

/// <summary>
/// Validates logical blob paths stored in the database (not full MinIO object keys).
/// </summary>
public static class BlobStoragePathHelper
{
    private const int MaxPathLength = 512;

    // Logical path: optional host/tenants prefix + folder/file segments (no spaces, no base64).
    // Allow uppercase in file/folder names (legacy imports e.g. user-signature-images/Images/.../CHUKY-1.png).
    private static readonly Regex ValidLogicalPathPattern = new(
        @"^(?:(?:host|tenants/[0-9a-fA-F-]{36})/)?[a-zA-Z0-9][a-zA-Z0-9._/-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsBlobStoragePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();

        if (trimmed.Length > MaxPathLength)
        {
            return false;
        }

        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject raw base64 stored by mistake (no path separators).
        if (!trimmed.Contains('/') && !trimmed.Contains('\\'))
        {
            return false;
        }

        return ValidLogicalPathPattern.IsMatch(trimmed.Replace('\\', '/'));
    }

    public static string SanitizeFileName(string fileName, int maxLength = 200)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "file";
        }

        if (name.Length <= maxLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        var allowedBaseLength = Math.Max(1, maxLength - extension.Length);
        if (baseName.Length > allowedBaseLength)
        {
            baseName = baseName[..allowedBaseLength];
        }

        return baseName + extension;
    }
}
