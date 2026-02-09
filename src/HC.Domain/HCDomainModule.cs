using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HC.Localization;
using HC.MultiTenancy;
using System;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Caching;
using HC.BlobStoring;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Commercial.SuiteTemplates;
using Volo.Abp.LanguageManagement;
using Volo.FileManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Saas;
using Volo.Abp.Gdpr;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Forms;

namespace HC;

[DependsOn(
    typeof(HCDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpEmailingModule),
    typeof(AbpIdentityProDomainModule),
    typeof(AbpOpenIddictProDomainModule),
    typeof(SaasDomainModule),
    typeof(TextTemplateManagementDomainModule),
    typeof(LanguageManagementDomainModule),
    typeof(FileManagementDomainModule),
    typeof(VoloAbpCommercialSuiteTemplatesModule),
    typeof(AbpGdprDomainModule),
    typeof(AbpBlobStoringMinioModule),
    typeof(FormsDomainModule)
    )]
public class HCDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("HC.Chat", typeof(HCResource));
        });

        // ABP đã có sẵn DefaultMinioBlobNameCalculator với logic tương tự
        // Uncomment dòng dưới nếu muốn dùng custom calculator với logging/tracking
        // context.Services.Replace(ServiceDescriptor.Transient<IMinioBlobNameCalculator, HCMinioBlobNameCalculator>());

#if DEBUG
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
    }
}
