using System.Threading.Tasks;
using Asp.Versioning;
using HC.Identity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HC.Controllers.Identity;

[RemoteService]
[Area("app")]
[ControllerName("Users")]
[Route("api/app/users")]
public class UsersController : AbpControllerBase, IUsersAppService
{
    private readonly IUsersAppService _usersAppService;

    public UsersController(IUsersAppService usersAppService)
    {
        _usersAppService = usersAppService;
    }

    [HttpGet]
    [Route("list-with-navigation-properties")]
    public Task<PagedResultDto<IdentityUserWithNavigationPropertiesDto>> GetListWithNavigationPropertiesAsync(GetUsersInput input)
    {
        return _usersAppService.GetListWithNavigationPropertiesAsync(input);
    }
}
