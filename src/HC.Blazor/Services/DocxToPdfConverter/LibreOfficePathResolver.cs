using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace HC.Blazor.Services.DocxToPdfConverter
{
    public static class LibreOfficePathResolver
    {
        public static string GetSofficePath(IConfiguration config, ILogger logger)
        {
            // 1. Ưu tiên lấy từ cấu hình (env/appsettings)
            var configured = config["LibreOffice:SofficePath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                logger.LogInformation("Using configured soffice path: {Path}", configured);
                return configured;
            }

            // 2. Auto detect theo OS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Bạn có thể thêm nhiều path fallback nếu cần
                var winPath = @"C:\Program Files\LibreOffice\program\soffice.exe";
                return winPath;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Mặc định LibreOffice trên mac
                // var macPath = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
                var macPath = "/opt/homebrew/bin/soffice";
                return macPath;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Trong Docker Linux, apt-get đã add vào PATH
                return "soffice";
            }

            throw new PlatformNotSupportedException("Unsupported OS for LibreOffice/soffice.");
        }
    }
}
