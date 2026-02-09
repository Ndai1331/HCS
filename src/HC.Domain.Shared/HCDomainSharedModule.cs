using HC.Localization;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Validation.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.OpenIddict;
using Volo.Abp.LanguageManagement;
using Volo.FileManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Saas;
using Volo.Abp.Gdpr;
using Volo.Abp.GlobalFeatures;
using Volo.Forms;

namespace HC;

[DependsOn(
    typeof(AbpAuditLoggingDomainSharedModule),
    typeof(AbpBackgroundJobsDomainSharedModule),
    typeof(AbpFeatureManagementDomainSharedModule),
    typeof(AbpPermissionManagementDomainSharedModule),
    typeof(AbpSettingManagementDomainSharedModule),
    typeof(AbpIdentityProDomainSharedModule),
    typeof(AbpOpenIddictProDomainSharedModule),
    typeof(LanguageManagementDomainSharedModule),
    typeof(FileManagementDomainSharedModule),
    typeof(SaasDomainSharedModule),
    typeof(TextTemplateManagementDomainSharedModule),
    typeof(AbpGdprDomainSharedModule),
    typeof(AbpGlobalFeaturesModule),
    typeof(FormsDomainSharedModule)
    )]
public class HCDomainSharedModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        HCGlobalFeatureConfigurator.Configure();
        HCModuleExtensionConfigurator.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<HCDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<HCResource>("vi")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/HC");

            options.DefaultResourceType = typeof(HCResource);
            
            options.Languages.Add(new LanguageInfo("vi", "vi", "Vietnamese")); 
            options.Languages.Add(new LanguageInfo("en", "en", "English")); 

        });
        
        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("HC", typeof(HCResource));
        });
    }
}
