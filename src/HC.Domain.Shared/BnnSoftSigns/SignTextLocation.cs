using Amazon.Runtime;
using Bnn.SignLib;
using Bnnsoft.Sdk;
using iTextSharp.text;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace HC.BnnSoftSigns
{
    public class SignText
    {
        public string ApiKey { get; set; }
        public string Secret { get; set; }
        public string Uri { get; set; }
        readonly VinHsmServiceClient SignClient;
        private readonly ILogger<SignText>? _logger;
        private const string defaultUrl = "https://sign-hn10.vin-hsm.com";

        public SignText(string apiKey, string secret, string uri, ILogger<SignText>? logger = null)
        {
            //Thông tin tài khoản
            ApiKey = apiKey;
            Secret = secret;
            Uri = uri;
            _logger = logger;
            FontFactory.RegisterDirectory("font");
            SignClient = new VinHsmServiceClient(new BasicAWSCredentials(ApiKey, Secret), new SignserverConfig()
            {
                ServiceURL = Uri
            });
        }

        public byte[]? SignTextLocationCustomize(SignPdfInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger?.LogInformation(
                    "[SIGN_START] Starting PDF signing process | PdfSize: {PdfSize} bytes | SignatureName: {SignatureName} | PageSign: {PageSign} | TypeSignature: {TypeSignature}",
                    input.datapdf?.Length ?? 0,
                    input.signaturename,
                    input.pagesign,
                    input.typesignature);
                _logger?.LogInformation(
                    "[SIGN_ASSET] Asset bytes before signing | LayoutBytes={LayoutBytes} | SealBytes={SealBytes} | SignatureBytes={SignatureBytes}",
                    input.anhkhung?.Length ?? 0,
                    input.condau?.Length ?? 0,
                    input.chukytuoi?.Length ?? 0);

                SignByte signByte = new SignByte(SignClient);
                byte[]? bytes = input.datapdf;
                var pdf = new PdfHash(bytes ?? Array.Empty<byte>(), signByte);

                _logger?.LogDebug("[SIGN_PROCESS] PDF hash created | PdfSize: {PdfSize} bytes", bytes?.Length ?? 0);

                CertStore certStore = CertStore.Instance;
                var chain = certStore.getX509Chain(SignClient);
                var cert = chain[0];

                _logger?.LogDebug("[SIGN_PROCESS] Certificate retrieved | ChainLength: {ChainLength}", chain?.Length ?? 0);

                // Validate and normalize input parameters
                if (input.typesignature < 1 || input.typesignature > 3)
                {
                    _logger?.LogWarning("[SIGN_VALIDATION] Invalid typesignature, defaulting to 1 | OriginalValue: {OriginalValue}", input.typesignature);
                    input.typesignature = 1;
                }

                if (string.IsNullOrWhiteSpace(input.signaturename))
                {
                    _logger?.LogDebug("[SIGN_VALIDATION] Signature name is empty, using default: KySoDienTu");
                    input.signaturename = "KySoDienTu";
                }

                if (input.pagesign < 1)
                {
                    _logger?.LogDebug("[SIGN_VALIDATION] PageSign is less than 1, defaulting to 1 | OriginalValue: {OriginalValue}", input.pagesign);
                    input.pagesign = 1;
                }

                _logger?.LogInformation(
                    "[SIGN_PROCESS] Signing PDF with thumbnail | SignatureName: {SignatureName} | PageSign: {PageSign} | TypeSignature: {TypeSignature} | HashAlg: {HashAlg} | Position: ({XPoint}, {YPoint}) | Size: {Width}x{Height}",
                    input.signaturename,
                    input.pagesign,
                    input.typesignature,
                    input.hashalg,
                    input.xpoint,
                    input.ypoint,
                    input.width,
                    input.height);

                byte[]? signedPdf;
                using (SignGraphicRuntimeContext.UseLayout(input.anhkhung))
                {
                    var signed = pdf.SignPdfHashThumbnail(cert,
                                                          chain,
                                                          DateTime.Now,
                                                          input.hashalg,
                                                          input.typesignature,
                                                          input.condau,
                                                          input.chukytuoi,
                                                          input.nguoiky,
                                                          input.chucvu,
                                                          input.signaturename,
                                                          input.pagesign,
                                                          input.xpoint,
                                                          input.ypoint,
                                                          input.width,
                                                          input.height,
                                                          input.imgwidth,
                                                          input.imgheight,
                                                          input.borderstyle,
                                                          input.bordercolor,
                                                          input.fontcolor,
                                                          input.fontname,
                                                          input.fontstyle,
                                                          input.fontsize,
                                                          input.tylecondau,
                                                          input.tylechukytuoi,
                                                          input.textscale,
                                                          input.padleft,
                                                          input.padtop,
                                                          new GraphicCreator());
                    signedPdf = signed?.Pdf;
                }

                stopwatch.Stop();

                if (signedPdf == null)
                {
                    _logger?.LogError(
                        "[SIGN_ERROR] Signing returned null result | Duration: {DurationMs}ms | SignatureName: {SignatureName}",
                        stopwatch.ElapsedMilliseconds,
                        input.signaturename);
                    return null;
                }

                _logger?.LogInformation(
                    "[SIGN_SUCCESS] PDF signed successfully | Duration: {DurationMs}ms | OriginalSize: {OriginalSize} bytes | SignedSize: {SignedSize} bytes | SignatureName: {SignatureName}",
                    stopwatch.ElapsedMilliseconds,
                    bytes?.Length ?? 0,
                    signedPdf.Length,
                    input.signaturename);

                return signedPdf;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex,
                    "[SIGN_ERROR] Exception during PDF signing | Duration: {DurationMs}ms | SignatureName: {SignatureName} | ErrorMessage: {ErrorMessage} | StackTrace: {StackTrace}",
                    stopwatch.ElapsedMilliseconds,
                    input.signaturename,
                    ex.Message,
                    ex.StackTrace);
                
                // Fallback to console if logger is not available
                if (_logger == null)
                {
                    Console.Error.WriteLine($"SignText.SignTextLocationCustomize exception: {ex}");
                }
                
                return null;
            }
        }
    
        public byte[]? SignTextLocationCustomizeV2(SignPdfInput input, int xOffset = 0, int yOffset = 0)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger?.LogInformation(
                    "[SIGN_START_V2] Starting PDF signing by placeholder | PdfSize: {PdfSize} bytes | SignatureName: {SignatureName} | PageSign: {PageSign} | TypeSignature: {TypeSignature} | TextSign: {TextSign} | Offset: ({XOffset}, {YOffset})",
                    input.datapdf?.Length ?? 0,
                    input.signaturename,
                    input.pagesign,
                    input.typesignature,
                    input.textsign,
                    xOffset,
                    yOffset);
                _logger?.LogInformation(
                    "[SIGN_ASSET_V2] Asset bytes before placeholder signing | LayoutBytes={LayoutBytes} | SealBytes={SealBytes} | SignatureBytes={SignatureBytes}",
                    input.anhkhung?.Length ?? 0,
                    input.condau?.Length ?? 0,
                    input.chukytuoi?.Length ?? 0);

                SignByte signByte = new SignByte(SignClient);
                byte[] bytes = input.datapdf ?? Array.Empty<byte>();
                var pdf = new PdfHash(bytes, signByte);
                _logger?.LogDebug("[SIGN_PROCESS_V2] PDF hash created | PdfSize: {PdfSize} bytes", bytes.Length);

                if (input.typesignature < 1 || input.typesignature > 3)
                {
                    _logger?.LogWarning("[SIGN_VALIDATION_V2] Invalid typesignature, defaulting to 1 | OriginalValue: {OriginalValue}", input.typesignature);
                    input.typesignature = 1;
                }

                if (string.IsNullOrWhiteSpace(input.signaturename))
                {
                    _logger?.LogDebug("[SIGN_VALIDATION_V2] Signature name is empty, using default: KySoDienTu");
                    input.signaturename = "KySoDienTu";
                }

                if (string.IsNullOrWhiteSpace(input.textsign))
                {
                    _logger?.LogError("[SIGN_ERROR_V2] textsign is empty, cannot locate placeholder.");
                    throw new SignPlaceholderNotFoundException(input.textsign ?? string.Empty, input.pagesign);
                }

                if (input.pagesign == 0)
                {
                    // Keep -1 as "all pages" for placeholder search.
                    input.pagesign = -1;
                }

                var locs = pdf.GetTextLocations(input.textsign, input.pagesign);
                _logger?.LogInformation("[SIGN_PROCESS_V2] Placeholder search done | TextSign: {TextSign} | Matches: {MatchCount}", input.textsign, locs?.Count ?? 0);

                if (locs != null && locs.Count > 0)
                {
                    var loc = locs[0];
                    // Calculate the center of the text rectangle
                    input.xpoint = (int)loc.Rectangle.Left - xOffset / 2 - input.width / 2;
                    input.ypoint = yOffset + (int)loc.Rectangle.Bottom - input.height;
                    input.pagesign = loc.Page;

                    _logger?.LogInformation(
                        "[SIGN_PROCESS_V2] Placeholder selected | Page: {Page} | Rect: ({Left},{Bottom},{Right},{Top}) | FinalPosition: ({XPoint},{YPoint}) | Size: {Width}x{Height}",
                        loc.Page,
                        loc.Rectangle.Left,
                        loc.Rectangle.Bottom,
                        loc.Rectangle.Right,
                        loc.Rectangle.Top,
                        input.xpoint,
                        input.ypoint,
                        input.width,
                        input.height);

                    CertStore certStore = CertStore.Instance;
                    var chain = certStore.getX509Chain(SignClient);
                    var cert = chain[0];
                    _logger?.LogDebug("[SIGN_PROCESS_V2] Certificate retrieved | ChainLength: {ChainLength}", chain?.Length ?? 0);

                    _logger?.LogInformation(
                        "[SIGN_PROCESS_V2] Signing PDF with thumbnail | SignatureName: {SignatureName} | PageSign: {PageSign} | TypeSignature: {TypeSignature} | HashAlg: {HashAlg} | Position: ({XPoint}, {YPoint}) | Size: {Width}x{Height}",
                        input.signaturename,
                        input.pagesign,
                        input.typesignature,
                        input.hashalg,
                        input.xpoint,
                        input.ypoint,
                        input.width,
                        input.height);

                    byte[]? signedPdf;
                    using (SignGraphicRuntimeContext.UseLayout(input.anhkhung))
                    {
                        var signed = pdf.SignPdfHashThumbnail(cert, chain, DateTime.Now, input.hashalg, input.typesignature, input.condau, input.chukytuoi, input.nguoiky, input.chucvu, input.signaturename,
                            input.pagesign, input.xpoint, input.ypoint, input.width, input.height, input.imgwidth, input.imgheight, input.borderstyle, input.bordercolor, input.fontcolor, input.fontname, input.fontstyle, input.fontsize, input.tylecondau, input.tylechukytuoi, input.textscale, input.padleft, input.padtop,
                            new GraphicCreator());
                        signedPdf = signed?.Pdf;
                    }

                    stopwatch.Stop();
                    if (signedPdf == null)
                    {
                        _logger?.LogError(
                            "[SIGN_ERROR_V2] Signing returned null result | Duration: {DurationMs}ms | SignatureName: {SignatureName}",
                            stopwatch.ElapsedMilliseconds,
                            input.signaturename);
                        return null;
                    }

                    _logger?.LogInformation(
                        "[SIGN_SUCCESS_V2] PDF signed successfully | Duration: {DurationMs}ms | OriginalSize: {OriginalSize} bytes | SignedSize: {SignedSize} bytes | SignatureName: {SignatureName}",
                        stopwatch.ElapsedMilliseconds,
                        bytes.Length,
                        signedPdf.Length,
                        input.signaturename);

                    return signedPdf;
                }

                stopwatch.Stop();
                _logger?.LogError(
                    "[SIGN_ERROR_V2] Placeholder not found | Duration: {DurationMs}ms | TextSign: {TextSign} | PageSign: {PageSign}",
                    stopwatch.ElapsedMilliseconds,
                    input.textsign,
                    input.pagesign);
                throw new SignPlaceholderNotFoundException(input.textsign ?? string.Empty, input.pagesign);
            }
            catch (SignPlaceholderNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex,
                    "[SIGN_ERROR_V2] Exception during placeholder signing | Duration: {DurationMs}ms | SignatureName: {SignatureName} | ErrorMessage: {ErrorMessage} | StackTrace: {StackTrace}",
                    stopwatch.ElapsedMilliseconds,
                    input.signaturename,
                    ex.Message,
                    ex.StackTrace);

                if (_logger == null)
                {
                    Console.Error.WriteLine($"SignText.SignTextLocationCustomizeV2 exception: {ex}");
                }
            }

            return null;
        }
    }
}

