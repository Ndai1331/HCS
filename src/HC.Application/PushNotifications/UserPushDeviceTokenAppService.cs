using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace HC.PushNotifications;

[Authorize]
public class UserPushDeviceTokenAppService : HCAppService, IUserPushDeviceTokenAppService
{
    private readonly IRepository<UserPushDeviceToken, Guid> _repository;

    public UserPushDeviceTokenAppService(IRepository<UserPushDeviceToken, Guid> repository)
    {
        _repository = repository;
    }

    public virtual async Task<UserPushDeviceTokenDto> RegisterAsync(RegisterPushDeviceTokenDto input)
    {
        var userId = CurrentUser.GetId();
        var q = await _repository.GetQueryableAsync();

        UserPushDeviceToken? entity = null;
        if (!string.IsNullOrWhiteSpace(input.DeviceId))
        {
            entity = await AsyncExecuter.FirstOrDefaultAsync(
                q.Where(x => x.UserId == userId && x.DeviceId == input.DeviceId));
        }

        entity ??= await AsyncExecuter.FirstOrDefaultAsync(
            q.Where(x => x.UserId == userId && x.FcmToken == input.Token));

        if (entity != null)
        {
            entity.UpdateToken(input.Token);
            entity.Platform = input.Platform;
            if (!string.IsNullOrWhiteSpace(input.DeviceId))
            {
                entity.DeviceId = input.DeviceId;
            }

            await _repository.UpdateAsync(entity, autoSave: true);
        }
        else
        {
            entity = new UserPushDeviceToken(
                GuidGenerator.Create(),
                userId,
                input.Token,
                input.Platform,
                input.DeviceId,
                CurrentTenant.Id);
            await _repository.InsertAsync(entity, autoSave: true);
        }

        return new UserPushDeviceTokenDto { Id = entity.Id };
    }

    public virtual async Task UnregisterAsync(Guid id)
    {
        var userId = CurrentUser.GetId();
        var entity = await _repository.GetAsync(id);
        if (entity.UserId != userId)
        {
            throw new AbpAuthorizationException(
                "You can only unregister your own push device tokens.");
        }

        await _repository.DeleteAsync(entity);
    }
}
