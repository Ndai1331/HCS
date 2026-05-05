using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Localization.Resources.AbpUi;
using Microsoft.Extensions.Configuration;
using HC.Localization;
using HC.Permissions;
using Volo.Abp.Account.Localization;
using Volo.Abp.UI.Navigation;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.SettingManagement.Blazor.Menus;
using Volo.Abp.Identity.Pro.Blazor.Navigation;
using Volo.Abp.AuditLogging.Blazor.Menus;
using Volo.Abp.LanguageManagement.Blazor.Menus;
using Volo.FileManagement.Blazor.Navigation;
using Volo.Abp.TextTemplateManagement.Blazor.Menus;
using Volo.Abp.OpenIddict.Pro.Blazor.Menus;
using Volo.Saas.Host.Blazor.Navigation;
using HC.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace HC.Blazor.Menus;

public class HCMenuContributor : IMenuContributor
{
    private readonly IConfiguration _configuration;

    public HCMenuContributor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            var reportMenuDataProvider = context.ServiceProvider.GetRequiredService<IReportMenuDataProvider>();
            var reports = reportMenuDataProvider.GetCachedReports();

            ConfigureMainMenuAsync(context, reports);
        }
        else if (context.Menu.Name == StandardMenus.User)
        {
            await ConfigureUserMenuAsync(context);
        }
        else if (context.Menu.Name == StandardMenus.Shortcut)
        {
            await ConfigureMobileMenuAsync(context);
        }
    }

    private void ConfigureMainMenuAsync(MenuConfigurationContext context, IReadOnlyList<ReportDto> reports)
    {
        var l = context.GetLocalizer<HCResource>();
        context.Menu.Items.Insert(0, new ApplicationMenuItem(HCMenus.Home, l["Menu:Home"], "/", icon: "fas fa-home", order: 1));
        context.Menu.AddItem(new ApplicationMenuItem("Documents", l["Menu:Documents"], icon: "fa fa-book", order: 2).AddItem(new ApplicationMenuItem(HCMenus.ArchiveDocuments, l["Menu:ArchiveDocuments"], icon: "bi bi-menu-button-wide-fill", url: "/manage-documents?sourceType=0").RequirePermissions(HCPermissions.Documents.Default)).AddItem(new ApplicationMenuItem(HCMenus.PersonalDocuments, l["Menu:PersonalDocuments"], icon: "bi bi-file-person-fill", url: "/manage-documents?sourceType=1").RequirePermissions(HCPermissions.Documents.Default)).AddItem(new ApplicationMenuItem(HCMenus.DocumentsSentToMe, l["Menu:DocumentsSentToMe"], icon: "bi bi-inbox-fill", url: "/manage-documents?sourceType=2").RequirePermissions(new string[] { HCPermissions.Documents.Default, HCPermissions.DocumentAssignments.Default })).AddItem(new ApplicationMenuItem(HCMenus.DocumentSigning, l["Menu:DocumentSigning"], icon: "bi bi-pen-fill", url: "/document-signing").RequirePermissions(HCPermissions.Documents.SubmitForSigning)));
        context.Menu.AddItem(new ApplicationMenuItem("Workflows", l["Menu:Workflows"], icon: "fa fa-arrow-trend-up", order: 3).AddItem(new ApplicationMenuItem("Workflows.WorkflowDefinitions", l["Menu:WorkflowDefinitions"], icon: "bi bi-menu-button-wide-fill", url: "/workflow-definitions").RequirePermissions(HCPermissions.WorkflowDefinitions.Default)).AddItem(new ApplicationMenuItem("Workflows.List", l["Menu:WorkflowList"], icon: "bi bi-menu-button-wide-fill", url: "/workflow-lists").RequirePermissions(HCPermissions.Workflows.Default)));
        context.Menu.AddItem(new ApplicationMenuItem("Projects", l["Menu:Projects"], icon: "fa fa-diagram-project", order: 4).AddItem(new ApplicationMenuItem("Projects.List", l["Menu:ProjectList"], icon: "bi bi-menu-button-wide-fill", url: "/projects").RequirePermissions(HCPermissions.Projects.Default)).AddItem(new ApplicationMenuItem("Tasks.List", l["Menu:Tasks"], icon: "bi bi-menu-button-wide-fill", url: "/tasks").RequirePermissions(HCPermissions.Tasks.Default)));
        context.Menu.AddItem(new ApplicationMenuItem("CalendarAndEvents", l["Menu:CalendarAndEvents"], icon: "fa fa-calendar-days", url: "/calendar-events", order: 6).RequirePermissions(HCPermissions.CalendarEvents.Default));
        context.Menu.AddItem(new ApplicationMenuItem("SurveyResults", l["Menu:Survey"], icon: "fa fa-chart-line", url: "/survey-results", order: 6).RequirePermissions(HCPermissions.SurveyResults.Default));
        context.Menu.AddItem(new ApplicationMenuItem("MasterDatas", l["Menu:Categories"], icon: "fa fa-layer-group", order: 9).AddItem(new ApplicationMenuItem("MasterDatas.DocumentTypes", l["DocumentTypes"], icon: "bi bi-menu-button-wide-fill", url: "/document-types").RequirePermissions(HCPermissions.MasterDatas.DocumentTypeDefault)).AddItem(new ApplicationMenuItem("MasterDatas.Sector", l["Sector"], icon: "bi bi-menu-button-wide-fill", url: "/sectors").RequirePermissions(HCPermissions.MasterDatas.SectorDefault)).AddItem(new ApplicationMenuItem("MasterDatas.UrgencyLevel", l["UrgencyLevel"], icon: "bi bi-menu-button-wide-fill", url: "/urgency-levels").RequirePermissions(HCPermissions.MasterDatas.UrgencyLevelDefault)).AddItem(new ApplicationMenuItem("MasterDatas.ConfidentialityLevel", l["ConfidentialityLevel"], icon: "bi bi-menu-button-wide-fill", url: "/confidentiality-levels").RequirePermissions(HCPermissions.MasterDatas.ConfidentialityLevelDefault)).AddItem(new ApplicationMenuItem("MasterDatas.ProcessingMethod", l["ProcessingMethod"], icon: "bi bi-menu-button-wide-fill", url: "/processing-methods").RequirePermissions(HCPermissions.MasterDatas.ProcessingMethodDefault)).AddItem(new ApplicationMenuItem("MasterDatas.DocumentStatus", l["DocumentStatus"], icon: "bi bi-menu-button-wide-fill", url: "/document-status").RequirePermissions(HCPermissions.MasterDatas.DocumentStatusDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SigningMethod", l["SigningMethod"], icon: "bi bi-menu-button-wide-fill", url: "/signing-methods").RequirePermissions(HCPermissions.MasterDatas.SigningMethodDefault)).AddItem(new ApplicationMenuItem("MasterDatas.EventType", l["EventType"], icon: "bi bi-menu-button-wide-fill", url: "/even-types").RequirePermissions(HCPermissions.MasterDatas.EventTypeDefault))// .AddItem(new ApplicationMenuItem("MasterDatas.IssuingAuthority", l["IssuingAuthority"], url: "/unit-lists").RequirePermissions(HCPermissions.MasterDatas.UnitDefault))
        .AddItem(new ApplicationMenuItem("MasterDatas.Unit", l["Unit"], icon: "bi bi-menu-button-wide-fill", url: "/unit-lists").RequirePermissions(HCPermissions.MasterDatas.UnitDefault))
        .AddItem(new ApplicationMenuItem("MasterDatas.Departments", l["Menu:Departments"], icon: "bi bi-menu-button-wide-fill", url: "/departments").RequirePermissions(HCPermissions.MasterDatas.DepartmentDefault)).AddItem(new ApplicationMenuItem("MasterDatas.Positions", l["Menu:Positions"], icon: "bi bi-menu-button-wide-fill", url: "/positions").RequirePermissions(HCPermissions.MasterDatas.PositionDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SurveyLocations", l["Menu:SurveyLocations"], icon: "bi bi-menu-button-wide-fill", url: "/survey-locations").RequirePermissions(HCPermissions.MasterDatas.SurveyLocationDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SurveyCriterias", l["Menu:SurveyCriterias"], icon: "bi bi-menu-button-wide-fill", url: "/survey-criterias").RequirePermissions(HCPermissions.MasterDatas.SurveyCriteriaDefault))
        
        .AddItem(new ApplicationMenuItem("MasterDatas.SignatureSettings", l["Menu:SignatureSettings"], icon: "bi bi-menu-button-wide-fill", url: "/signature-settings").RequirePermissions(HCPermissions.MasterDatas.SignatureSettingsDefault))
        .AddItem(new ApplicationMenuItem("MasterDatas.Reports", l["Menu:Reports"], icon: "bi bi-menu-button-wide-fill", url: "/reports").RequirePermissions(HCPermissions.Reports.Default)));


        var reportsMenu = new ApplicationMenuItem("Reports", l["Menu:Reports"], icon: "fa fa-chart-area", order: 12)
            .RequirePermissions(HCPermissions.Reports.Default);

        foreach (var report in reports)
        {
            reportsMenu.AddItem(
                new ApplicationMenuItem(
                    name: "DynamicReportItem" + report.Id,
                    displayName: report.Name,
                    url: $"/report-web-frame?reportId={report.Id}",
                    icon: "bi bi-menu-button-wide-fill" 
                )
            );
        }

        context.Menu.AddItem(reportsMenu);



        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 15;
        //Administration->Identity
        administration.SetSubItemOrder(IdentityProMenus.GroupName, 2);
        //Administration->OpenIddict
        administration.SetSubItemOrder(OpenIddictProMenus.GroupName, 3);
        //Administration->Language Management
        administration.SetSubItemOrder(LanguageManagementMenus.GroupName, 5);
        //Administration->Text Template Management
        administration.SetSubItemOrder(TextTemplateManagementMenus.GroupName, 6);
        //Administration->Audit Logs
        administration.SetSubItemOrder(AbpAuditLoggingMenus.GroupName, 7);
        //Administration->Settings
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 8);
        //Administration->Saas
        administration.SetSubItemOrder(SaasHostMenus.GroupName, 9);
        // administration.SetSubItemOrder(FormsMenus.GroupName, 10);
        //Saas
        // context.Menu.SetSubItemOrder(SaasHostMenus.GroupName, 5);
        // context.Menu.SetSubItemOrder(FileManagementMenuNames.GroupName, 5);
        context.Menu.TryRemoveMenuItem(FileManagementMenuNames.GroupName);
        // context.Menu.TryRemoveMenuItem(SaasHostMenus.GroupName);
        context.Menu.TryRemoveMenuItem(IdentityProMenus.ClaimTypes);
        context.Menu.TryRemoveMenuItem(IdentityProMenus.OrganizationUnits);
        context.Menu.TryRemoveMenuItem(IdentityProMenus.SecurityLogs);
        //Administration->Saas
        return;
    }

    private Task ConfigureUserMenuAsync(MenuConfigurationContext context)
    {
        var uiResource = context.GetLocalizer<AbpUiResource>();
        var hcResource = context.GetLocalizer<HCResource>();
        var accountResource = context.GetLocalizer<AccountResource>();
        var authServerUrl = _configuration["AuthServer:Authority"] ?? "~";
        context.Menu.AddItem(new ApplicationMenuItem("Menu:Personal", hcResource["Menu:Personal"], url: "~/my-profile", icon: "bi-sliders", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Menu:FileManagement", hcResource["Menu:FileManagement"], url: "~/file-management", icon: "fa fa-file-alt", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Menu:Notifications", hcResource["Menu:Notifications"], url: "~/notification-receivers", icon: "fa fa-bell", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem(HCMenus.Chat, hcResource["Menu:Chat"], url: "~/chat", icon: "bi bi-chat-dots-fill", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Account.Logout", uiResource["Logout"], url: "~/Account/Logout", icon: "fa fa-power-off", order: int.MaxValue - 1000).RequireAuthenticated());
        return Task.CompletedTask;
    }

    private Task ConfigureMobileMenuAsync(MenuConfigurationContext context)
    {
        var hcResource = context.GetLocalizer<HCResource>();
        context.Menu.AddItem(new ApplicationMenuItem(HCMenus.Home, hcResource["Menu:Home"], url: "~/", icon: "bi bi-house-fill", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem(HCMenus.Chat, hcResource["Menu:Chat"], url: "~/chat", icon: "bi bi-chat-dots-fill", order: int.MaxValue - 1000).RequireAuthenticated());
        return Task.CompletedTask;
    }
}