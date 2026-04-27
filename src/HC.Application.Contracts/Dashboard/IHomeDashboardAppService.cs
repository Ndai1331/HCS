using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HC.Dashboard;

public interface IHomeDashboardAppService : IApplicationService
{
    Task<HomeDashboardBundleDto> GetDashboardBundleAsync(GetHomeDashboardBundleInput input);
}
