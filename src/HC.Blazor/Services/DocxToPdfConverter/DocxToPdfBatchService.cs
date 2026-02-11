using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Services.DocxToPdfConverter
{
    public sealed class DocxToPdfBatchService
    {
        private readonly IDocxToPdfConverter _converter;
        private readonly ILogger<DocxToPdfBatchService> _logger;

        public DocxToPdfBatchService(
            IDocxToPdfConverter converter,
            ILogger<DocxToPdfBatchService> logger)
        {
            _converter = converter;
            _logger = logger;
        }

        /// <summary>
        /// Convert nhiều DOCX sang PDF song song nhưng có giới hạn.
        /// </summary>
        public async Task<Dictionary<string, byte[]>> ConvertManyAsync(
            IEnumerable<string> inputDocxPaths,
            int maxParallel = 4,
            CancellationToken ct = default)
        {
            if (maxParallel < 1)
                maxParallel = 1;

            var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            var result = new ConcurrentDictionary<string, byte[]>();
            var tasks = new List<Task>();

            foreach (var path in inputDocxPaths)
            {
                // Bắt early token
                ct.ThrowIfCancellationRequested();

                await semaphore.WaitAsync(ct).ConfigureAwait(false);

                var localPath = path;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var pdfBytes = await _converter.ConvertFileAsync(localPath, ct).ConfigureAwait(false);
                        result[localPath] = pdfBytes;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Convert DOCX failed for file: {File}", localPath);
                        // Tùy yêu cầu: có thể throw để fail cả batch hoặc ignore từng file
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new Dictionary<string, byte[]>(result);
        }
    }

}
