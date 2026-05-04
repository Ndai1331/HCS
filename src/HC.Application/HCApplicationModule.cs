using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AuditLogging;
using Volo.Abp.Gdpr;
using Volo.Abp.LanguageManagement;
using Volo.FileManagement;
using Volo.Abp.OpenIddict;
using Volo.Abp.TextTemplateManagement;
using Volo.Saas.Host;
// using Volo.Forms;
using PdfSharp.Fonts;
using HC.Helpers;
using Volo.Abp.Elsa;
namespace HC;

[DependsOn(
    typeof(HCDomainModule),
    typeof(HCApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountPublicApplicationModule),
    typeof(AbpAccountAdminApplicationModule),
    typeof(SaasHostApplicationModule),
    typeof(AbpAuditLoggingApplicationModule),
    typeof(TextTemplateManagementApplicationModule),
    typeof(AbpOpenIddictProApplicationModule),
    typeof(LanguageManagementApplicationModule),
    typeof(FileManagementApplicationModule),
    typeof(AbpGdprApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    typeof(AbpMapperlyModule),
    typeof(AbpBackgroundJobsModule)
    // typeof(FormsApplicationModule)
    )]
[DependsOn(typeof(AbpElsaApplicationModule))]
    public class HCApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<HCApplicationModule>();

        if (GlobalFontSettings.FontResolver == null || GlobalFontSettings.FontResolver is not CustomFontResolver)
        {
            GlobalFontSettings.FontResolver = new CustomFontResolver();
        }
    }
}
