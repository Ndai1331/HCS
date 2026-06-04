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
using HC.DocumentWorkflowInstances;
using HC.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

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
    public class HCApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<HCApplicationModule>();
        Configure<WorkflowSigningOptions>(context.Services.GetConfiguration().GetSection(WorkflowSigningOptions.SectionName));

        if (GlobalFontSettings.FontResolver == null || GlobalFontSettings.FontResolver is not CustomFontResolver)
        {
            GlobalFontSettings.FontResolver = new CustomFontResolver();
        }

        context.Services.AddTransient<IDocumentSigningQueryService, DocumentSigningQueryService>();
        context.Services.AddTransient<IWorkflowSubmissionService, WorkflowSubmissionService>();
        context.Services.AddTransient<IDocumentSigningFilterQueryBuilder, DocumentSigningFilterQueryBuilder>();
        context.Services.AddTransient<IWorkflowSubmitInfoQueryService, WorkflowSubmitInfoQueryService>();
        context.Services.AddTransient<IWorkflowDocumentFileService, WorkflowDocumentFileService>();
        context.Services.AddTransient<IWorkflowNotificationService, WorkflowNotificationService>();
        context.Services.AddTransient<IWorkflowActionService, WorkflowActionService>();
        context.Services.AddTransient<IWorkflowCommittedStepsQueryService, WorkflowCommittedStepsQueryService>();
        context.Services.AddTransient<IWorkflowInstanceQueryService, WorkflowInstanceQueryService>();
        context.Services.AddTransient<IWorkflowSignerManagementService, WorkflowSignerManagementService>();
        context.Services.AddTransient<IWorkflowOverdueExtensionService, WorkflowOverdueExtensionService>();
        context.Services.AddTransient<IDocumentSigningExportService, DocumentSigningExportService>();
        context.Services.AddTransient<IWorkflowDisplayPdfResolver, WorkflowDisplayPdfResolver>();
    }
}
