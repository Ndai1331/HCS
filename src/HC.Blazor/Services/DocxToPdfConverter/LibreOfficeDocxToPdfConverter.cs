using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HC.Blazor.Services.DocxToPdfConverter
{
    public sealed class LibreOfficeDocxToPdfConverter : IDocxToPdfConverter
    {
        private readonly ILogger<LibreOfficeDocxToPdfConverter> _logger;
        private readonly string _sofficePath;
        private readonly string _unoconvPath;
        private readonly string _unoHost;
        private readonly int _unoPort;

        // Giới hạn tối đa số process LibreOffice chạy song song
        private readonly SemaphoreSlim _globalSemaphore;
        private bool _unoconvAvailable = false;

        public LibreOfficeDocxToPdfConverter(
            IConfiguration config,
            ILogger<LibreOfficeDocxToPdfConverter> logger)
        {
            _logger = logger;

            _sofficePath = LibreOfficePathResolver.GetSofficePath(config, logger);
            _unoconvPath = config["LibreOffice:UnoconvPath"] ?? "/opt/homebrew/bin/unoconv";
            _unoHost = config["LibreOffice:UnoHost"] ?? "127.0.0.1";
            _unoPort = config.GetValue("LibreOffice:UnoPort", 2002);

            // Attempt to detect if unoconv is executable
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = _unoconvPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(start);
                if (proc != null)
                {
                    var exited = proc.WaitForExit(5000);
                    if (exited && proc.ExitCode == 0)
                    {
                        _unoconvAvailable = true;
                        _logger.LogInformation("unoconv found. Using UNO mode.");
                    }
                    else
                    {
                        // Kill process if it didn't exit in time
                        if (!exited)
                        {
                            try { proc.Kill(); } catch { /* ignore */ }
                        }
                        _logger.LogWarning("unoconv executable test failed (exited={Exited}). Fallback to soffice.", exited);
                    }
                }
                else
                {
                    _logger.LogWarning("unoconv process could not be started. Fallback to soffice.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "unoconv detection failed. Fallback to soffice.");
            }

            // Adjust concurrency dynamically
            var cpuCount = Environment.ProcessorCount;
            var maxParallel = _unoconvAvailable && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? Math.Clamp(Math.Min(cpuCount * 2, 16), 4, 16)  // concurrency allowed
                : 1;

            _globalSemaphore = new SemaphoreSlim(maxParallel, maxParallel);

            _logger.LogInformation("LibreOffice converter configured with maxParallel={maxParallel}, unoconvAvailable={unoconvAvailable}",
                maxParallel, _unoconvAvailable);
        }

        public async Task<byte[]> ConvertFileAsync(string inputDocxPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(inputDocxPath))
                throw new ArgumentException("inputDocxPath is null or empty.", nameof(inputDocxPath));

            if (!File.Exists(inputDocxPath))
                throw new FileNotFoundException($"DOCX file not found: {inputDocxPath}", inputDocxPath);

            await _globalSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_unoconvAvailable)
                    try
                    {
                        return await ConvertWithUnoconvAsync(inputDocxPath, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "unoconv convert failed, falling back to soffice for file: {File}", inputDocxPath);
                    }

                // Mặc định / Windows
                return await ConvertWithSofficeAsync(inputDocxPath, ct).ConfigureAwait(false);
            }
            finally
            {
                _globalSemaphore.Release();
            }
        }

        private async Task<byte[]> ConvertWithSofficeAsync(string inputDocxPath, CancellationToken ct)
        {
            var inputFullPath = Path.GetFullPath(inputDocxPath);

            // Tạo thư mục tạm riêng cho mỗi convert
            var tempRoot = Path.GetTempPath();
            var tempDir = Path.Combine(tempRoot, "lo-convert", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            _logger.LogDebug("Converting DOCX to PDF: {Input} -> TempDir: {TempDir}",
                inputFullPath, tempDir);

            try
            {
                // Arguments cho soffice: headless, convert sang pdf, outdir là tempDir
                var psi = new ProcessStartInfo
                {
                    FileName = _sofficePath,
                    Arguments = $"--headless --nologo --nodefault --norestore --nolockcheck --nofirststartwizard --convert-to pdf --outdir \"{tempDir}\" \"{inputFullPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        stderr.AppendLine(e.Data);
                };

                if (!process.Start())
                    throw new InvalidOperationException("Failed to start LibreOffice process.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Chờ process xong, có hỗ trợ cancellation
                await WaitForExitAsync(process, ct).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _logger.LogError(
                        "LibreOffice exited with code {ExitCode}. StdOut: {StdOut} StdErr: {StdErr}",
                        process.ExitCode, stdout.ToString(), stderr.ToString());

                    throw new InvalidOperationException(
                        $"LibreOffice convert failed (exit code {process.ExitCode}). Error: {stderr}");
                }

                // Tìm file pdf output trong tempDir
                var pdfFile = Directory.EnumerateFiles(tempDir, "*.pdf").FirstOrDefault();
                if (pdfFile == null)
                {
                    _logger.LogError(
                        "No PDF file generated for input: {Input}. StdOut: {StdOut}. StdErr: {StdErr}",
                        inputFullPath, stdout.ToString(), stderr.ToString());

                    throw new FileNotFoundException("PDF output not found after conversion.");
                }

                var bytes = await File.ReadAllBytesAsync(pdfFile, ct).ConfigureAwait(false);
                return bytes;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup LibreOffice temp directory: {TempDir}", tempDir);
                }
            }
        }

        private async Task<byte[]> ConvertWithUnoconvAsync(string inputDocxPath, CancellationToken ct)
        {
            var inputFullPath = Path.GetFullPath(inputDocxPath);

            var tempRoot = Path.Combine(Path.GetTempPath(), "lo-convert");
            Directory.CreateDirectory(tempRoot);

            var outputPdfPath = Path.Combine(
                tempRoot,
                $"{Path.GetFileNameWithoutExtension(inputFullPath)}-{Guid.NewGuid():N}.pdf");

            _logger.LogDebug(
                "Converting DOCX to PDF (unoconv/UNO): {Input} -> {Output} via {Host}:{Port}",
                inputFullPath, outputPdfPath, _unoHost, _unoPort);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _unoconvPath,
                    Arguments =
                        $"--server {_unoHost} --port {_unoPort} " +
                        $"-f pdf -o \"{outputPdfPath}\" \"{inputFullPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        stderr.AppendLine(e.Data);
                };

                if (!process.Start())
                    throw new InvalidOperationException("Failed to start unoconv process.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await WaitForExitAsync(process, ct).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _logger.LogError(
                        "unoconv exited with code {ExitCode}. StdOut: {StdOut} StdErr: {StdErr}",
                        process.ExitCode, stdout.ToString(), stderr.ToString());

                    if (process.ExitCode == 251)
                    {
                        throw new InvalidOperationException(
                            $"unoconv failed with exit code 251 (cannot connect UNO listener at {_unoHost}:{_unoPort}). " +
                            "Check that soffice UNO listener is running and reachable from this container.");
                    }

                    throw new InvalidOperationException(
                        $"unoconv convert failed (exit code {process.ExitCode}). Error: {stderr}");
                }

                if (!File.Exists(outputPdfPath))
                {
                    _logger.LogError(
                        "PDF output not found after unoconv convert. Input: {Input}. StdOut: {StdOut}. StdErr: {StdErr}",
                        inputFullPath, stdout.ToString(), stderr.ToString());

                    throw new FileNotFoundException("PDF output not found after conversion.");
                }

                var bytes = await File.ReadAllBytesAsync(outputPdfPath, ct).ConfigureAwait(false);
                return bytes;
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPdfPath))
                        File.Delete(outputPdfPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup LibreOffice temp PDF file: {Pdf}", outputPdfPath);
                }
            }
        }

        private static Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
        {
            if (process.HasExited)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object?>();

            process.Exited += (_, _) => tcs.TrySetResult(null);
            process.EnableRaisingEvents = true;

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch { /* ignore */ }

                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }
    }
}
