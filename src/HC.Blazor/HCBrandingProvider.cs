using Microsoft.Extensions.Localization;
using HC.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace HC.Blazor;

[Dependency(ReplaceServices = true)]
public class HCBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<HCResource> _localizer;

    public HCBrandingProvider(IStringLocalizer<HCResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
    public override string LogoUrl => "/images/logo/logo.png";
}
