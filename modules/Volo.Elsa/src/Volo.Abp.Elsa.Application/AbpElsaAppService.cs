using Volo.Abp.Elsa.Localization;
using Volo.Abp.Application.Services;

namespace Volo.Abp.Elsa;

public abstract class AbpElsaAppService : ApplicationService
{
    protected AbpElsaAppService()
    {
        LocalizationResource = typeof(AbpElsaResource);
        ObjectMapperContext = typeof(AbpElsaApplicationModule);
    }
}
