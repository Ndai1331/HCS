using HC.DocumentWorkflowInstanceLogss;
using HC.DocumentWorkflowInstanceFiles;
using HC.UserDepartments;
using HC.SurveyResults;
using HC.SurveyFiles;
using HC.SurveySessions;
using HC.SurveyCriterias;
using HC.SurveyLocations;
using HC.CalendarEventParticipants;
using HC.CalendarEvents;
using HC.UserSignatures;
using HC.SignatureSettings;
using HC.NotificationReceivers;
using HC.Notifications;
using HC.ProjectTaskDocuments;
using HC.ProjectTaskAssignments;
using HC.ProjectTasks;
using HC.ProjectMembers;
using HC.Projects;
using HC.DocumentHistories;
using HC.DocumentAssignments;
using HC.DocumentWorkflowInstances;
using HC.DocumentFiles;
using HC.Documents;
using HC.WorkflowStepAssignments;
using HC.Units;
using HC.Departments;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.WorkflowDefinitions;
using HC.MasterDatas;
using HC.Positions;
using System;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Uow;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.LanguageManagement.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TextTemplateManagement.EntityFrameworkCore;
using Volo.Saas.EntityFrameworkCore;
using Volo.FileManagement.EntityFrameworkCore;
using Volo.Abp.Gdpr;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.Studio;
using Volo.Forms.EntityFrameworkCore;

namespace HC.EntityFrameworkCore;

[DependsOn(typeof(HCDomainModule), typeof(AbpIdentityProEntityFrameworkCoreModule), typeof(AbpOpenIddictProEntityFrameworkCoreModule), typeof(AbpPermissionManagementEntityFrameworkCoreModule), typeof(AbpSettingManagementEntityFrameworkCoreModule), typeof(AbpEntityFrameworkCorePostgreSqlModule), typeof(AbpBackgroundJobsEntityFrameworkCoreModule), typeof(AbpAuditLoggingEntityFrameworkCoreModule), typeof(AbpFeatureManagementEntityFrameworkCoreModule), typeof(LanguageManagementEntityFrameworkCoreModule), typeof(FileManagementEntityFrameworkCoreModule), typeof(SaasEntityFrameworkCoreModule), typeof(TextTemplateManagementEntityFrameworkCoreModule), typeof(AbpGdprEntityFrameworkCoreModule))]
[DependsOn(typeof(FormsEntityFrameworkCoreModule))]
    public class HCEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        HCEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<HCDbContext>(options => {
            /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<Position, Positions.EfCorePositionRepository>();
            options.AddRepository<MasterData, MasterDatas.EfCoreMasterDataRepository>();
            options.AddRepository<WorkflowDefinition, WorkflowDefinitions.EfCoreWorkflowDefinitionRepository>();
            options.AddRepository<Workflow, Workflows.EfCoreWorkflowRepository>();
            options.AddRepository<WorkflowTemplate, WorkflowTemplates.EfCoreWorkflowTemplateRepository>();
            options.AddRepository<WorkflowStepTemplate, WorkflowStepTemplates.EfCoreWorkflowStepTemplateRepository>();
            options.AddRepository<Department, Departments.EfCoreDepartmentRepository>();
            options.AddRepository<Unit, Units.EfCoreUnitRepository>();
            options.AddRepository<WorkflowStepAssignment, WorkflowStepAssignments.EfCoreWorkflowStepAssignmentRepository>();
            options.AddRepository<Document, Documents.EfCoreDocumentRepository>();
            options.AddRepository<DocumentFile, DocumentFiles.EfCoreDocumentFileRepository>();
            options.AddRepository<DocumentWorkflowInstance, DocumentWorkflowInstances.EfCoreDocumentWorkflowInstanceRepository>();
            options.AddRepository<DocumentAssignment, DocumentAssignments.EfCoreDocumentAssignmentRepository>();
            options.AddRepository<DocumentHistory, DocumentHistories.EfCoreDocumentHistoryRepository>();
            options.AddRepository<Project, Projects.EfCoreProjectRepository>();
            options.AddRepository<ProjectMember, ProjectMembers.EfCoreProjectMemberRepository>();
            options.AddRepository<ProjectTask, ProjectTasks.EfCoreProjectTaskRepository>();
            options.AddRepository<ProjectTaskAssignment, ProjectTaskAssignments.EfCoreProjectTaskAssignmentRepository>();
            options.AddRepository<ProjectTaskDocument, ProjectTaskDocuments.EfCoreProjectTaskDocumentRepository>();
            options.AddRepository<Notification, Notifications.EfCoreNotificationRepository>();
            options.AddRepository<NotificationReceiver, NotificationReceivers.EfCoreNotificationReceiverRepository>();
            options.AddRepository<SignatureSetting, SignatureSettings.EfCoreSignatureSettingRepository>();
            options.AddRepository<UserSignature, UserSignatures.EfCoreUserSignatureRepository>();
            options.AddRepository<CalendarEvent, CalendarEvents.EfCoreCalendarEventRepository>();
            options.AddRepository<CalendarEventParticipant, CalendarEventParticipants.EfCoreCalendarEventParticipantRepository>();
            options.AddRepository<SurveyLocation, SurveyLocations.EfCoreSurveyLocationRepository>();
            options.AddRepository<SurveyCriteria, SurveyCriterias.EfCoreSurveyCriteriaRepository>();
            options.AddRepository<SurveySession, SurveySessions.EfCoreSurveySessionRepository>();
            options.AddRepository<SurveyFile, SurveyFiles.EfCoreSurveyFileRepository>();
            options.AddRepository<SurveyResult, SurveyResults.EfCoreSurveyResultRepository>();
            options.AddRepository<UserDepartment, UserDepartments.EfCoreUserDepartmentRepository>();
            // Chat repositories
            options.AddRepository<HC.Chat.Messages.Message, HC.Chat.EntityFrameworkCore.Messages.EfCoreMessageRepository>();
            options.AddRepository<HC.Chat.Messages.UserMessage, HC.Chat.EntityFrameworkCore.Messages.EfCoreUserMessageRepository>();
            options.AddRepository<HC.Chat.Users.ChatUser, HC.Chat.EntityFrameworkCore.Users.EfCoreChatUserRepository>();
            options.AddRepository<HC.Chat.Conversations.Conversation, HC.Chat.EntityFrameworkCore.Conversations.EfCoreConversationRepository>();
            options.AddRepository<HC.Chat.Conversations.ConversationMember, HC.Chat.EntityFrameworkCore.Conversations.EfCoreConversationMemberRepository>();
            options.AddRepository<HC.Chat.Messages.MessageFile, HC.Chat.EntityFrameworkCore.Messages.EfCoreMessageFileRepository>();
            options.AddRepository<DocumentWorkflowInstanceFile, DocumentWorkflowInstanceFiles.EfCoreDocumentWorkflowInstanceFileRepository>();
            options.AddRepository<DocumentWorkflowInstanceLogs, DocumentWorkflowInstanceLogss.EfCoreDocumentWorkflowInstanceLogsRepository>();
        });
        // Register IChatDbContext mapping to HCDbContext
        // This allows repositories that depend on IChatDbContext to resolve HCDbContext
        context.Services.AddScoped<HC.Chat.EntityFrameworkCore.IChatDbContext>(sp => sp.GetRequiredService<HCDbContext>());
        context.Services.AddAbpDbContext<HCTenantDbContext>(options => {
            /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
        });
        if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
        {
            return;
        }

        Configure<AbpDbContextOptions>(options => {
            /* The main point to change your DBMS.
            * See also HCDbContextFactoryBase for EF Core tooling. */
            options.UseNpgsql();
        });
    }
}