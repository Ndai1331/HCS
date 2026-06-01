using System.Threading.Tasks;
using Volo.Abp.Content;

namespace HC.DocumentWorkflowInstances;

public interface IDocumentSigningExportService
{
    Task<IRemoteStreamContent> GetDocumentSigningListAsExcelFileAsync(DocumentSigningExcelDownloadDto input);
}
