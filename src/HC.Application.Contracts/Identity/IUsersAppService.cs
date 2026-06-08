using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HC.Identity;

public interface IUsersAppService : IApplicationService
{
    Task<PagedResultDto<IdentityUserWithNavigationPropertiesDto>> GetListWithNavigationPropertiesAsync(GetUsersInput input);
}
