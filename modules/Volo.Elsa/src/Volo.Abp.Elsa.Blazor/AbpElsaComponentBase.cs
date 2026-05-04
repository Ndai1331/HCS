using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Elsa.Localization;

namespace Volo.Abp.Elsa;

public abstract class AbpElsaComponentBase : AbpComponentBase
{
    protected AbpElsaComponentBase()
    {
        LocalizationResource = typeof(AbpElsaResource);
    }
}
