using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Content;

namespace HC.DocumentWorkflowInstances;

public partial class DocumentWorkflowInstancesAppService
{
    [AllowAnonymous]
    public Task<IRemoteStreamContent> GetDocumentSigningListAsExcelFileAsync(DocumentSigningExcelDownloadDto input)
        => _documentSigningExportService.GetDocumentSigningListAsExcelFileAsync(input);
}
