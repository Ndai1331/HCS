using Volo.Abp.Elsa.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Volo.Abp.Elsa;

public abstract class ElsaController : AbpControllerBase
{
    protected ElsaController()
    {
        LocalizationResource = typeof(AbpElsaResource);
    }
}
