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

                stopwatch.Stop();

                if (signed?.Pdf == null)
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
                    signed.Pdf.Length,
                    input.signaturename);

                return signed.Pdf;
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
    }
}

