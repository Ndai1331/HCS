using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HC.PushNotifications;

public interface IUserPushDeviceTokenAppService : IApplicationService
{
    Task<UserPushDeviceTokenDto> RegisterAsync(RegisterPushDeviceTokenDto input);

    Task UnregisterAsync(Guid id);
}
