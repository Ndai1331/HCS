using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace HC.Reports;

public partial interface IReportsAppService
{
    Task<PagedResultDto<ReportDto>> GetListForNavigationAsync(GetReportsInput input);
}