using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using Localization.Resources.AbpUi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HC.Localization;
using HC.Permissions;
using HC.MultiTenancy;
using Volo.Abp.Users;
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
using Volo.FileManagement.Blazor.Navigation;
using HC.Chat.Authorization;

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
            await ConfigureMainMenuAsync(context);
        }
        else if (context.Menu.Name == StandardMenus.User)
        {
            await ConfigureUserMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<HCResource>();
        context.Menu.Items.Insert(0, new ApplicationMenuItem(HCMenus.Home, l["Menu:Home"], "/", icon: "fas fa-home", order: 1));
        // FileManagement menu is removed from UI (kept as a module dependency).
        context.Menu.AddItem(new ApplicationMenuItem("Documents", l["Menu:Documents"], icon: "fa fa-book", order: 2).AddItem(new ApplicationMenuItem("Documents.List", l["Menu:DocumentList"], url: "/documents").RequirePermissions(HCPermissions.Documents.Default)));
        context.Menu.AddItem(new ApplicationMenuItem("Workflows", l["Menu:Workflows"], icon: "fa fa-arrow-trend-up", order: 3).AddItem(new ApplicationMenuItem("Workflows.WorkflowDefinitions", l["Menu:WorkflowDefinitions"], url: "/workflow-definitions").RequirePermissions(HCPermissions.WorkflowDefinitions.Default)).AddItem(new ApplicationMenuItem("Workflows.List", l["Menu:WorkflowList"], url: "/workflow-lists").RequirePermissions(HCPermissions.Workflows.Default)));
        context.Menu.AddItem(new ApplicationMenuItem("Projects", l["Menu:Projects"], icon: "fa fa-diagram-project", order: 4).AddItem(new ApplicationMenuItem("Projects.List", l["Menu:ProjectList"], url: "/projects").RequirePermissions(HCPermissions.Projects.Default)).AddItem(new ApplicationMenuItem("Tasks.List", l["Menu:Tasks"], url: "/tasks").RequirePermissions(HCPermissions.Tasks.Default)));
        context.Menu.AddItem(new ApplicationMenuItem("CalendarAndEvents", l["Menu:CalendarAndEvents"], icon: "fa fa-calendar-days", order: 6).AddItem(new ApplicationMenuItem("CalendarAndEvents.CalendarAndEvents", l["Menu:CalendarAndEvents"], url: "/calendar-events").RequirePermissions(HCPermissions.CalendarEvents.Default)));
        // context.Menu.AddItem(new ApplicationMenuItem("Personal", l["Menu:Personal"], icon: "fa fa-users", order: 5)
        // .AddItem(new ApplicationMenuItem("Users.Signatures", l["Menu:Signatures"], url: "/user-signatures").RequirePermissions(HCPermissions.UserSignatures.Default))
        // .AddItem(new ApplicationMenuItem("Users.Departments", l["Menu:Departments"], url: "/user-departments").RequirePermissions(HCPermissions.Departments.Default))
        // .AddItem(new ApplicationMenuItem("Users.Notifications", l["Menu:Notifications"], url: "/notification-receivers").RequirePermissions(HCPermissions.Notifications.Default))
        // .AddItem(new ApplicationMenuItem("Users.Chats", l["Menu:Chats"], url: "/chat").RequirePermissions(ChatPermissions.Messaging)));

        context.Menu.AddItem(new ApplicationMenuItem("SurveyResults", l["Menu:SurveyResults"], icon: "fa fa-chart-line", order: 6).AddItem(new ApplicationMenuItem("SurveyResults.SurveyResults", l["Menu:SurveyResults"], url: "/survey-results").RequirePermissions(HCPermissions.SurveyResults.Default)));
        context.Menu.AddItem(new ApplicationMenuItem("MasterDatas", l["Menu:Categories"], icon: "fa fa-layer-group", order: 9).AddItem(new ApplicationMenuItem("MasterDatas.DocumentTypes", l["DocumentTypes"], url: "/document-types").RequirePermissions(HCPermissions.MasterDatas.DocumentTypeDefault)).AddItem(new ApplicationMenuItem("MasterDatas.Sector", l["Sector"], url: "/sectors").RequirePermissions(HCPermissions.MasterDatas.SectorDefault))
        // .AddItem(
        .AddItem(new ApplicationMenuItem("MasterDatas.UrgencyLevel", l["UrgencyLevel"], url: "/urgency-levels").RequirePermissions(HCPermissions.MasterDatas.UrgencyLevelDefault)).AddItem(new ApplicationMenuItem("MasterDatas.ConfidentialityLevel", l["ConfidentialityLevel"], url: "/confidentiality-levels").RequirePermissions(HCPermissions.MasterDatas.ConfidentialityLevelDefault)).AddItem(new ApplicationMenuItem("MasterDatas.ProcessingMethod", l["ProcessingMethod"], url: "/processing-methods").RequirePermissions(HCPermissions.MasterDatas.ProcessingMethodDefault)).AddItem(new ApplicationMenuItem("MasterDatas.DocumentStatus", l["DocumentStatus"], url: "/document-status").RequirePermissions(HCPermissions.MasterDatas.DocumentStatusDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SigningMethod", l["SigningMethod"], url: "/signing-methods").RequirePermissions(HCPermissions.MasterDatas.SigningMethodDefault)).AddItem(new ApplicationMenuItem("MasterDatas.EventType", l["EventType"], url: "/even-types").RequirePermissions(HCPermissions.MasterDatas.EventTypeDefault))// .AddItem(new ApplicationMenuItem("MasterDatas.IssuingAuthority", l["IssuingAuthority"], url: "/unit-lists").RequirePermissions(HCPermissions.MasterDatas.UnitDefault))
        .AddItem(new ApplicationMenuItem("MasterDatas.Unit", l["Unit"], url: "/unit-lists").RequirePermissions(HCPermissions.MasterDatas.UnitDefault)).AddItem(new ApplicationMenuItem("MasterDatas.Departments", l["Menu:Departments"], url: "/departments").RequirePermissions(HCPermissions.MasterDatas.DepartmentDefault)).AddItem(new ApplicationMenuItem("MasterDatas.Positions", l["Menu:Positions"], url: "/positions").RequirePermissions(HCPermissions.MasterDatas.PositionDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SurveyLocations", l["Menu:SurveyLocations"], url: "/survey-locations").RequirePermissions(HCPermissions.MasterDatas.SurveyLocationDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SurveyCriterias", l["Menu:SurveyCriterias"], url: "/survey-criterias").RequirePermissions(HCPermissions.MasterDatas.SurveyCriteriaDefault)).AddItem(new ApplicationMenuItem("MasterDatas.SignatureSettings", l["Menu:SignatureSettings"], url: "/signature-settings").RequirePermissions(HCPermissions.MasterDatas.SignatureSettingsDefault)));
        context.Menu.AddItem(new ApplicationMenuItem("Reports", l["Menu:Reports"], icon: "fa fa-chart-area", order: 12).AddItem(new ApplicationMenuItem("Reports.Documents", l["Documents"], url: "/reports-documents").RequirePermissions(HCPermissions.Reports.DocumentDefault)).AddItem(new ApplicationMenuItem("Reports.Projects", l["Projects"], url: "/reports-projects").RequirePermissions(HCPermissions.Reports.ProjectDefault)).AddItem(new ApplicationMenuItem("Reports.ProjectTasks", l["ProjectTasks"], url: "/reports-project-tasks").RequirePermissions(HCPermissions.Reports.ProjectTaskDefault)).AddItem(new ApplicationMenuItem("Reports.Workflows", l["Workflows"], url: "/reports-workflows").RequirePermissions(HCPermissions.Reports.WorkflowDefault)));
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
        // context.Menu.SetSubItemOrder(FileManagementMenuNames.GroupName, 5);
        context.Menu.TryRemoveMenuItem(FileManagementMenuNames.GroupName);
        context.Menu.TryRemoveMenuItem(SaasHostMenus.GroupName);
        return Task.CompletedTask;
    }

    private Task ConfigureUserMenuAsync(MenuConfigurationContext context)
    {
        var uiResource = context.GetLocalizer<AbpUiResource>();
        var hcResource = context.GetLocalizer<HCResource>();
        var accountResource = context.GetLocalizer<AccountResource>();
        var authServerUrl = _configuration["AuthServer:Authority"] ?? "~";
        // context.Menu.AddItem(new ApplicationMenuItem("Account.Manage", accountResource["MyAccount"], $"{authServerUrl.EnsureEndsWith('/')}Account/Manage", icon: "fa fa-cog", order: 1000, target: "_blank").RequireAuthenticated());
        // context.Menu.AddItem(new ApplicationMenuItem("Account.SecurityLogs", accountResource["MySecurityLogs"], $"{authServerUrl.EnsureEndsWith('/')}Account/SecurityLogs", icon: "fa fa-user-shield", target: "_blank").RequireAuthenticated());
        // context.Menu.AddItem(new ApplicationMenuItem("Account.Sessions", accountResource["Sessions"], url: $"{authServerUrl.EnsureEndsWith('/')}Account/Sessions", icon: "fa fa-clock", target: "_blank").RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Menu:Personal", hcResource["Menu:Personal"], url: "~/my-profile", icon: "bi-sliders", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Menu:FileManagement", hcResource["Menu:FileManagement"], url: "~/file-management", icon: "fa fa-file-alt", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Menu:Notifications", hcResource["Menu:Notifications"], url: "~/notification-receivers", icon: "fa fa-bell", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Menu:Chats", hcResource["Menu:Chats"], url: "~/chat", icon: "bi bi-chat-dots-fill", order: int.MaxValue - 1000).RequireAuthenticated());
        context.Menu.AddItem(new ApplicationMenuItem("Account.Logout", uiResource["Logout"], url: "~/Account/Logout", icon: "fa fa-power-off", order: int.MaxValue - 1000).RequireAuthenticated());
        return Task.CompletedTask;
    }
}