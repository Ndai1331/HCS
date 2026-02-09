using HC.Localization;
using Volo.Abp.Application.Services;

namespace HC.Chat;

public abstract class ChatAppService : ApplicationService
{
    protected ChatAppService()
    {
        LocalizationResource = typeof(HCResource);
        ObjectMapperContext = typeof(HCApplicationModule);
    }
}
