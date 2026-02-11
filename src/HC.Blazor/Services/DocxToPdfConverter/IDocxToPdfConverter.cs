using System.Threading;
using System.Threading.Tasks;

namespace HC.Blazor.Services.DocxToPdfConverter
{
    public interface IDocxToPdfConverter
    {
        Task<byte[]> ConvertFileAsync(string inputDocxPath, CancellationToken ct = default);
    }
}
