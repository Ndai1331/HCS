using HC.Reports;
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
using Volo.Abp.Identity;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.WorkflowDefinitions;
using HC.MasterDatas;
using HC.Positions;
using HC.PushNotifications;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;

namespace HC.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class HCDbContext : HCDbContextBase<HCDbContext>, IHasEventInbox
{
    public DbSet<IncomingEventRecord> IncomingEvents { get; set; } = null!;
    public DbSet<UserPushDeviceToken> PushDeviceTokens { get; set; } = null!;
    public DbSet<Report> Reports { get; set; } = null!;
    public DbSet<DocumentWorkflowInstanceLogs> DocumentWorkflowInstanceLogss { get; set; } = null!;
    public DbSet<DocumentWorkflowInstanceFile> DocumentWorkflowInstanceFiles { get; set; } = null!;
    public DbSet<UserDepartment> UserDepartments { get; set; } = null!;
    public DbSet<SurveyResult> SurveyResults { get; set; } = null!;
    public DbSet<SurveyFile> SurveyFiles { get; set; } = null!;
    public DbSet<SurveySession> SurveySessions { get; set; } = null!;
    public DbSet<SurveyCriteria> SurveyCriterias { get; set; } = null!;
    public DbSet<SurveyLocation> SurveyLocations { get; set; } = null!;
    public DbSet<CalendarEventParticipant> CalendarEventParticipants { get; set; } = null!;
    public DbSet<CalendarEvent> CalendarEvents { get; set; } = null!;
    public DbSet<UserSignature> UserSignatures { get; set; } = null!;
    public DbSet<SignatureSetting> SignatureSettings { get; set; } = null!;
    public DbSet<NotificationReceiver> NotificationReceivers { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ProjectTaskDocument> ProjectTaskDocuments { get; set; } = null!;
    public DbSet<ProjectTaskAssignment> ProjectTaskAssignments { get; set; } = null!;
    public DbSet<ProjectTask> ProjectTasks { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<DocumentHistory> DocumentHistories { get; set; } = null!;
    public DbSet<DocumentAssignment> DocumentAssignments { get; set; } = null!;
    public DbSet<DocumentWorkflowInstance> DocumentWorkflowInstances { get; set; } = null!;
    public DbSet<DocumentFile> DocumentFiles { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<WorkflowStepAssignment> WorkflowStepAssignments { get; set; } = null!;
    public DbSet<Unit> Units { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<WorkflowStepTemplate> WorkflowStepTemplates { get; set; } = null!;
    public DbSet<WorkflowTemplate> WorkflowTemplates { get; set; } = null!;
    public DbSet<Workflow> Workflows { get; set; } = null!;
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; } = null!;
    public DbSet<MasterData> MasterDatas { get; set; } = null!;
    public DbSet<Position> Positions { get; set; } = null!;
    public DbSet<DocumentBackgroundOperation> DocumentBackgroundOperations { get; set; } = null!;
    public DbSet<NotificationOutbox> NotificationOutboxes { get; set; } = null!;

    // Chat DbSets are inherited from HCDbContextBase
    public HCDbContext(DbContextOptions<HCDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.SetMultiTenancySide(MultiTenancySides.Both);
        base.OnModelCreating(builder);
        builder.Entity<Position>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Positions", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Position.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(Position.Code)).IsRequired().HasMaxLength(PositionConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(Position.Name)).IsRequired();
            b.Property(x => x.SignOrder).HasColumnName(nameof(Position.SignOrder)).HasMaxLength(PositionConsts.SignOrderMaxLength);
            b.Property(x => x.IsActive).HasColumnName(nameof(Position.IsActive));
        });
        builder.Entity<MasterData>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "MasterDatas", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(MasterData.TenantId));
            b.Property(x => x.Type).HasColumnName(nameof(MasterData.Type)).IsRequired().HasMaxLength(MasterDataConsts.TypeMaxLength);
            b.Property(x => x.Code).HasColumnName(nameof(MasterData.Code)).IsRequired().HasMaxLength(MasterDataConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(MasterData.Name)).IsRequired();
            b.Property(x => x.SortOrder).HasColumnName(nameof(MasterData.SortOrder)).HasMaxLength(MasterDataConsts.SortOrderMaxLength);
            b.Property(x => x.IsActive).HasColumnName(nameof(MasterData.IsActive));
            // Speeds up DocumentStatusCode → MasterData.Id lookups (UpdateDocumentStatusAsync) and dropdown filtering by type.
            b.HasIndex(x => new { x.Type, x.Code }).HasDatabaseName("IX_AppMasterDatas_Type_Code");
        });
        builder.Entity<WorkflowDefinition>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "WorkflowDefinitions", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(WorkflowDefinition.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(WorkflowDefinition.Code)).IsRequired().HasMaxLength(WorkflowDefinitionConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(WorkflowDefinition.Name)).IsRequired();
            b.Property(x => x.Description).HasColumnName(nameof(WorkflowDefinition.Description));
            b.Property(x => x.IsActive).HasColumnName(nameof(WorkflowDefinition.IsActive));
        });
        builder.Entity<WorkflowTemplate>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "WorkflowTemplates", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(WorkflowTemplate.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(WorkflowTemplate.Code)).IsRequired().HasMaxLength(WorkflowTemplateConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(WorkflowTemplate.Name)).IsRequired();
            b.Property(x => x.WordTemplatePath).HasColumnName(nameof(WorkflowTemplate.WordTemplatePath));
            b.Property(x => x.PdfTemplatePath).HasColumnName(nameof(WorkflowTemplate.PdfTemplatePath));
            b.Property(x => x.ContentSchema).HasColumnName(nameof(WorkflowTemplate.ContentSchema));
            b.Property(x => x.OutputFormat).HasColumnName(nameof(WorkflowTemplate.OutputFormat)).HasMaxLength(WorkflowTemplateConsts.OutputFormatMaxLength);
            b.Property(x => x.SignMode).HasColumnName(nameof(WorkflowTemplate.SignMode)).HasMaxLength(WorkflowTemplateConsts.SignModeMaxLength);
            b.HasOne<Workflow>().WithMany().IsRequired().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<Department>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Departments", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Department.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(Department.Code)).IsRequired().HasMaxLength(DepartmentConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(Department.Name)).IsRequired();
            b.Property(x => x.ParentId).HasColumnName(nameof(Department.ParentId));
            b.Property(x => x.Level).HasColumnName(nameof(Department.Level));
            b.Property(x => x.SortOrder).HasColumnName(nameof(Department.SortOrder));
            b.Property(x => x.IsActive).HasColumnName(nameof(Department.IsActive));
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.LeaderUserId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<Unit>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Units", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Unit.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(Unit.Code)).IsRequired().HasMaxLength(UnitConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(Unit.Name)).IsRequired();
            b.Property(x => x.SortOrder).HasColumnName(nameof(Unit.SortOrder));
            b.Property(x => x.IsActive).HasColumnName(nameof(Unit.IsActive));
        });
        builder.Entity<DocumentHistory>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentHistories", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentHistory.TenantId));
            b.Property(x => x.Comment).HasColumnName(nameof(DocumentHistory.Comment));
            b.Property(x => x.Action).HasColumnName(nameof(DocumentHistory.Action)).IsRequired().HasMaxLength(DocumentHistoryConsts.ActionMaxLength);
            b.HasOne<Document>().WithMany().IsRequired().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.FromUser).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.ToUser).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<ProjectMember>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "ProjectMembers", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(ProjectMember.TenantId));
            b.Property(x => x.MemberRole).HasColumnName(nameof(ProjectMember.MemberRole)).IsRequired().HasMaxLength(ProjectMemberConsts.MemberRoleMaxLength);
            b.Property(x => x.JoinedAt).HasColumnName(nameof(ProjectMember.JoinedAt));
            b.HasOne<Project>().WithMany().IsRequired().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<ProjectTask>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "ProjectTasks", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(ProjectTask.TenantId));
            b.Property(x => x.ParentTaskId).HasColumnName(nameof(ProjectTask.ParentTaskId));
            b.Property(x => x.Code).HasColumnName(nameof(ProjectTask.Code)).IsRequired().HasMaxLength(ProjectTaskConsts.CodeMaxLength);
            b.Property(x => x.Title).HasColumnName(nameof(ProjectTask.Title)).IsRequired().HasMaxLength(ProjectTaskConsts.TitleMaxLength);
            b.Property(x => x.Description).HasColumnName(nameof(ProjectTask.Description));
            b.Property(x => x.StartDate).HasColumnName(nameof(ProjectTask.StartDate));
            b.Property(x => x.DueDate).HasColumnName(nameof(ProjectTask.DueDate));
            b.Property(x => x.Priority).HasColumnName(nameof(ProjectTask.Priority)).IsRequired().HasMaxLength(ProjectTaskConsts.PriorityMaxLength);
            b.Property(x => x.Status).HasColumnName(nameof(ProjectTask.Status)).IsRequired().HasMaxLength(ProjectTaskConsts.StatusMaxLength);
            b.Property(x => x.ProgressPercent).HasColumnName(nameof(ProjectTask.ProgressPercent)).HasMaxLength(ProjectTaskConsts.ProgressPercentMaxLength);
            b.HasOne<Project>().WithMany().IsRequired().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<ProjectTaskAssignment>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "ProjectTaskAssignments", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(ProjectTaskAssignment.TenantId));
            b.Property(x => x.AssignmentRole).HasColumnName(nameof(ProjectTaskAssignment.AssignmentRole)).IsRequired().HasMaxLength(ProjectTaskAssignmentConsts.AssignmentRoleMaxLength);
            b.Property(x => x.AssignedAt).HasColumnName(nameof(ProjectTaskAssignment.AssignedAt));
            b.Property(x => x.Note).HasColumnName(nameof(ProjectTaskAssignment.Note));
            b.HasOne<ProjectTask>().WithMany().IsRequired().HasForeignKey(x => x.ProjectTaskId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<ProjectTaskDocument>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "ProjectTaskDocuments", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(ProjectTaskDocument.TenantId));
            b.Property(x => x.DocumentPurpose).HasColumnName(nameof(ProjectTaskDocument.DocumentPurpose)).IsRequired().HasMaxLength(ProjectTaskDocumentConsts.DocumentPurposeMaxLength);
            b.HasOne<ProjectTask>().WithMany().IsRequired().HasForeignKey(x => x.ProjectTaskId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Document>().WithMany().IsRequired().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<Notification>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Notifications", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Notification.TenantId));
            b.Property(x => x.Title).HasColumnName(nameof(Notification.Title)).IsRequired();
            b.Property(x => x.Content).HasColumnName(nameof(Notification.Content)).IsRequired();
            b.Property(x => x.SourceType).HasColumnName(nameof(Notification.SourceType)).IsRequired().HasConversion<string>();
            b.Property(x => x.EventType).HasColumnName(nameof(Notification.EventType)).IsRequired().HasConversion<string>();
            b.Property(x => x.RelatedType).HasColumnName(nameof(Notification.RelatedType)).IsRequired().HasConversion<string>();
            b.Property(x => x.RelatedId).HasColumnName(nameof(Notification.RelatedId));
            b.Property(x => x.Priority).HasColumnName(nameof(Notification.Priority)).IsRequired();
        });
        builder.Entity<NotificationReceiver>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "NotificationReceivers", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(NotificationReceiver.TenantId));
            b.Property(x => x.IsRead).HasColumnName(nameof(NotificationReceiver.IsRead));
            b.Property(x => x.ReadAt).HasColumnName(nameof(NotificationReceiver.ReadAt));
            b.HasOne<Notification>().WithMany().IsRequired().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<SignatureSetting>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "SignatureSettings", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(SignatureSetting.TenantId));
            b.Property(x => x.ProviderCode).HasColumnName(nameof(SignatureSetting.ProviderCode)).IsRequired();
            b.Property(x => x.ProviderType).HasColumnName(nameof(SignatureSetting.ProviderType)).IsRequired();
            b.Property(x => x.ApiEndpoint).HasColumnName(nameof(SignatureSetting.ApiEndpoint)).IsRequired();
            b.Property(x => x.LayoutImg).HasColumnName(nameof(SignatureSetting.LayoutImg));
            b.Property(x => x.ApiTimeout).HasColumnName(nameof(SignatureSetting.ApiTimeout));
            b.Property(x => x.DefaultSignType).HasColumnName(nameof(SignatureSetting.DefaultSignType)).IsRequired();
            b.Property(x => x.AllowElectronicSign).HasColumnName(nameof(SignatureSetting.AllowElectronicSign));
            b.Property(x => x.AllowDigitalSign).HasColumnName(nameof(SignatureSetting.AllowDigitalSign));
            b.Property(x => x.RequireOtp).HasColumnName(nameof(SignatureSetting.RequireOtp));
            b.Property(x => x.SignWidth).HasColumnName(nameof(SignatureSetting.SignWidth));
            b.Property(x => x.SignHeight).HasColumnName(nameof(SignatureSetting.SignHeight));
            b.Property(x => x.SignedFileSuffix).HasColumnName(nameof(SignatureSetting.SignedFileSuffix)).IsRequired();
            b.Property(x => x.KeepOriginalFile).HasColumnName(nameof(SignatureSetting.KeepOriginalFile));
            b.Property(x => x.OverwriteSignedFile).HasColumnName(nameof(SignatureSetting.OverwriteSignedFile));
            b.Property(x => x.EnableSignLog).HasColumnName(nameof(SignatureSetting.EnableSignLog));
            b.Property(x => x.IsActive).HasColumnName(nameof(SignatureSetting.IsActive));
        });
        builder.Entity<UserSignature>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "UserSignatures", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(UserSignature.TenantId));
            b.Property(x => x.SignType).HasColumnName(nameof(UserSignature.SignType)).IsRequired();
            b.Property(x => x.ProviderCode).HasColumnName(nameof(UserSignature.ProviderCode)).IsRequired();
            b.Property(x => x.TokenRef).HasColumnName(nameof(UserSignature.TokenRef));
            b.Property(x => x.Secret).HasColumnName(nameof(UserSignature.Secret));
            b.Property(x => x.SealImg).HasColumnName(nameof(UserSignature.SealImg));
            b.Property(x => x.SignatureImage).HasColumnName(nameof(UserSignature.SignatureImage)).IsRequired();
            b.Property(x => x.ValidFrom).HasColumnName(nameof(UserSignature.ValidFrom));
            b.Property(x => x.ValidTo).HasColumnName(nameof(UserSignature.ValidTo));
            b.Property(x => x.IsActive).HasColumnName(nameof(UserSignature.IsActive));
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<CalendarEventParticipant>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "CalendarEventParticipants", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(CalendarEventParticipant.TenantId));
            b.Property(x => x.ResponseStatus).HasColumnName(nameof(CalendarEventParticipant.ResponseStatus)).IsRequired();
            b.Property(x => x.Notified).HasColumnName(nameof(CalendarEventParticipant.Notified));
            b.HasOne<CalendarEvent>().WithMany().IsRequired().HasForeignKey(x => x.CalendarEventId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<CalendarEvent>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "CalendarEvents", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(CalendarEvent.TenantId));
            b.Property(x => x.Title).HasColumnName(nameof(CalendarEvent.Title)).IsRequired();
            b.Property(x => x.Description).HasColumnName(nameof(CalendarEvent.Description));
            b.Property(x => x.StartTime).HasColumnName(nameof(CalendarEvent.StartTime));
            b.Property(x => x.EndTime).HasColumnName(nameof(CalendarEvent.EndTime));
            b.Property(x => x.AllDay).HasColumnName(nameof(CalendarEvent.AllDay));
            b.Property(x => x.EventType).HasColumnName(nameof(CalendarEvent.EventType)).IsRequired();
            b.Property(x => x.Location).HasColumnName(nameof(CalendarEvent.Location));
            b.Property(x => x.RelatedType).HasColumnName(nameof(CalendarEvent.RelatedType)).IsRequired();
            b.Property(x => x.RelatedId).HasColumnName(nameof(CalendarEvent.RelatedId));
            b.Property(x => x.Visibility).HasColumnName(nameof(CalendarEvent.Visibility)).IsRequired();
        });
        builder.Entity<Project>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Projects", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Project.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(Project.Code)).IsRequired().HasMaxLength(ProjectConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(Project.Name)).IsRequired().HasMaxLength(ProjectConsts.NameMaxLength);
            b.Property(x => x.Description).HasColumnName(nameof(Project.Description));
            b.Property(x => x.StartDate).HasColumnName(nameof(Project.StartDate));
            b.Property(x => x.EndDate).HasColumnName(nameof(Project.EndDate));
            b.Property(x => x.Status).HasColumnName(nameof(Project.Status)).IsRequired();
            b.HasOne<Department>().WithMany().HasForeignKey(x => x.OwnerDepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<SurveyLocation>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "SurveyLocations", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(SurveyLocation.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(SurveyLocation.Code)).IsRequired();
            b.Property(x => x.Name).HasColumnName(nameof(SurveyLocation.Name)).IsRequired();
            b.Property(x => x.Description).HasColumnName(nameof(SurveyLocation.Description));
            b.Property(x => x.IsActive).HasColumnName(nameof(SurveyLocation.IsActive));
        });
        builder.Entity<SurveyCriteria>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "SurveyCriterias", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(SurveyCriteria.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(SurveyCriteria.Code)).IsRequired();
            b.Property(x => x.Name).HasColumnName(nameof(SurveyCriteria.Name)).IsRequired();
            b.Property(x => x.Image).HasColumnName(nameof(SurveyCriteria.Image)).IsRequired();
            b.Property(x => x.DisplayOrder).HasColumnName(nameof(SurveyCriteria.DisplayOrder));
            b.Property(x => x.IsActive).HasColumnName(nameof(SurveyCriteria.IsActive));
            b.HasOne<SurveyLocation>().WithMany().IsRequired().HasForeignKey(x => x.SurveyLocationId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<SurveySession>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "SurveySessions", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(SurveySession.TenantId));
            b.Property(x => x.FullName).HasColumnName(nameof(SurveySession.FullName));
            b.Property(x => x.PhoneNumber).HasColumnName(nameof(SurveySession.PhoneNumber));
            b.Property(x => x.PatientCode).HasColumnName(nameof(SurveySession.PatientCode));
            b.Property(x => x.SurveyTime).HasColumnName(nameof(SurveySession.SurveyTime));
            b.Property(x => x.DeviceType).HasColumnName(nameof(SurveySession.DeviceType));
            b.Property(x => x.Note).HasColumnName(nameof(SurveySession.Note));
            b.Property(x => x.SessionDisplay).HasColumnName(nameof(SurveySession.SessionDisplay)).IsRequired();
            b.HasOne<SurveyLocation>().WithMany().IsRequired().HasForeignKey(x => x.SurveyLocationId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<SurveyFile>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "SurveyFiles", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(SurveyFile.TenantId));
            b.Property(x => x.UploaderType).HasColumnName(nameof(SurveyFile.UploaderType)).IsRequired();
            b.Property(x => x.FileName).HasColumnName(nameof(SurveyFile.FileName)).IsRequired();
            b.Property(x => x.FilePath).HasColumnName(nameof(SurveyFile.FilePath)).IsRequired();
            b.Property(x => x.FileSize).HasColumnName(nameof(SurveyFile.FileSize));
            b.Property(x => x.MimeType).HasColumnName(nameof(SurveyFile.MimeType)).IsRequired();
            b.Property(x => x.FileType).HasColumnName(nameof(SurveyFile.FileType)).IsRequired();
            b.HasOne<SurveySession>().WithMany().IsRequired().HasForeignKey(x => x.SurveySessionId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<SurveyResult>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "SurveyResults", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(SurveyResult.TenantId));
            b.Property(x => x.Rating).HasColumnName(nameof(SurveyResult.Rating));
            b.HasOne<SurveyCriteria>().WithMany().IsRequired().HasForeignKey(x => x.SurveyCriteriaId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<SurveySession>().WithMany().IsRequired().HasForeignKey(x => x.SurveySessionId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<Workflow>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Workflows", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Workflow.TenantId));
            b.Property(x => x.Code).HasColumnName(nameof(Workflow.Code)).IsRequired().HasMaxLength(WorkflowConsts.CodeMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(Workflow.Name)).IsRequired();
            b.Property(x => x.Description).HasColumnName(nameof(Workflow.Description));
            b.Property(x => x.IsActive).HasColumnName(nameof(Workflow.IsActive));
            b.HasOne<WorkflowDefinition>().WithMany().IsRequired().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<WorkflowStepAssignment>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "WorkflowStepAssignments", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(WorkflowStepAssignment.TenantId));
            b.Property(x => x.IsPrimary).HasColumnName(nameof(WorkflowStepAssignment.IsPrimary));
            b.Property(x => x.IsActive).HasColumnName(nameof(WorkflowStepAssignment.IsActive));
            b.HasOne<WorkflowStepTemplate>().WithMany().HasForeignKey(x => x.StepId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.DefaultUserId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<WorkflowStepTemplate>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "WorkflowStepTemplates", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(WorkflowStepTemplate.TenantId));
            b.Property(x => x.Order).HasColumnName(nameof(WorkflowStepTemplate.Order)).IsRequired().HasMaxLength(WorkflowStepTemplateConsts.OrderMaxLength);
            b.Property(x => x.Name).HasColumnName(nameof(WorkflowStepTemplate.Name)).IsRequired();
            b.Property(x => x.Type).HasColumnName(nameof(WorkflowStepTemplate.Type)).IsRequired().HasMaxLength(WorkflowStepTemplateConsts.TypeMaxLength);
            b.Property(x => x.SLADays).HasColumnName(nameof(WorkflowStepTemplate.SLADays));
            b.Property(x => x.AllowReturn).HasColumnName(nameof(WorkflowStepTemplate.AllowReturn));
            b.Property(x => x.IsActive).HasColumnName(nameof(WorkflowStepTemplate.IsActive));
            b.HasOne<WorkflowTemplate>().WithMany().IsRequired().HasForeignKey(x => x.WorkflowTemplateId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<UserDepartment>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "UserDepartments", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(UserDepartment.TenantId));
            b.Property(x => x.IsPrimary).HasColumnName(nameof(UserDepartment.IsPrimary));
            b.Property(x => x.IsActive).HasColumnName(nameof(UserDepartment.IsActive));
            b.HasOne<Department>().WithMany().IsRequired().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<Document>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "Documents", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(Document.TenantId));
            b.Property(x => x.No).HasColumnName(nameof(Document.No)).HasMaxLength(DocumentConsts.NoMaxLength);
            b.Property(x => x.Title).HasColumnName(nameof(Document.Title)).IsRequired();
            b.Property(x => x.CurrentStatus).HasColumnName(nameof(Document.CurrentStatus)).HasMaxLength(DocumentConsts.CurrentStatusMaxLength);
            b.Property(x => x.CompletedTime).HasColumnName(nameof(Document.CompletedTime));
            b.Property(x => x.StorageNumber).HasColumnName(nameof(Document.StorageNumber)).IsRequired().HasMaxLength(DocumentConsts.StorageNumberMaxLength);
            b.Property(x => x.IncommingDate).HasColumnName(nameof(Document.IncommingDate));
            b.Property(x => x.SourceType).HasColumnName(nameof(Document.SourceType)).IsRequired();
            b.Property(x => x.FromUserId).HasColumnName(nameof(Document.FromUserId));
            b.Property(x => x.ReceiverUserId).HasColumnName(nameof(Document.ReceiverUserId));
            b.Property(x => x.DepartmentId).HasColumnName(nameof(Document.DepartmentId));
            b.Property(x => x.OrganizationUnitId).HasColumnName(nameof(Document.OrganizationUnitId));
            b.Property(x => x.ParentDocumentId).HasColumnName(nameof(Document.ParentDocumentId));
            b.HasIndex(x => x.ParentDocumentId);
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.ParentDocumentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.SourceType);
            b.HasIndex(x => x.FromUserId);
            b.HasIndex(x => x.ReceiverUserId);
            b.HasIndex(x => x.DepartmentId);
            b.HasIndex(x => x.OrganizationUnitId);
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ReceiverUserId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            // Add index for filter performance
            b.HasOne<MasterData>().WithMany().HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Workflow>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<MasterData>().WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<MasterData>().WithMany().IsRequired().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MasterData>().WithMany().IsRequired().HasForeignKey(x => x.UrgencyLevelId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MasterData>().WithMany().IsRequired().HasForeignKey(x => x.SecrecyLevelId).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<DocumentAssignment>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentAssignments", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentAssignment.TenantId));
            b.Property(x => x.StepOrder).HasColumnName(nameof(DocumentAssignment.StepOrder)).HasMaxLength(DocumentAssignmentConsts.StepOrderMaxLength);
            b.Property(x => x.ActionType).HasColumnName(nameof(DocumentAssignment.ActionType)).IsRequired().HasMaxLength(DocumentAssignmentConsts.ActionTypeMaxLength);
            b.Property(x => x.Status).HasColumnName(nameof(DocumentAssignment.Status)).IsRequired().HasMaxLength(DocumentAssignmentConsts.StatusMaxLength);
            b.Property(x => x.AssignedAt).HasColumnName(nameof(DocumentAssignment.AssignedAt));
            b.Property(x => x.ProcessedAt).HasColumnName(nameof(DocumentAssignment.ProcessedAt));
            b.Property(x => x.IsCurrent).HasColumnName(nameof(DocumentAssignment.IsCurrent));
            b.HasOne<Document>().WithMany().IsRequired().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<WorkflowStepTemplate>().WithMany().HasForeignKey(x => x.WorkflowStepTemplateId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().IsRequired().HasForeignKey(x => x.ReceiverUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DocumentFile>().WithMany().HasForeignKey(x => x.DocumentFileResultId).OnDelete(DeleteBehavior.SetNull);
            // Filtered (partial) index – ApplyPendingApprovalFlagsAsync, GetSentToMeDocumentIdsAsync,
            // ApplySubmitSigningButtonVisibilityAsync, RevokeDocumentAsync all filter by IsCurrent = true on a document.
            b.HasIndex(x => new { x.DocumentId, x.IsCurrent }).HasDatabaseName("IX_AppDocumentAssignments_DocumentId_IsCurrent").HasFilter("\"IsCurrent\" = true");
        });
        builder.Entity<DocumentWorkflowInstanceFile>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentWorkflowInstanceFiles", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentWorkflowInstanceFile.TenantId));
            b.HasOne<DocumentFile>().WithMany().IsRequired().HasForeignKey(x => x.DocumentFileId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DocumentWorkflowInstance>().WithMany(x => x.DocumentWorkflowInstanceFiles).HasForeignKey(x => x.DocumentWorkflowInstanceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DocumentFile>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentFiles", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentFile.TenantId));
            b.Property(x => x.Name).HasColumnName(nameof(DocumentFile.Name)).IsRequired();
            b.Property(x => x.Path).HasColumnName(nameof(DocumentFile.Path));
            b.Property(x => x.Hash).HasColumnName(nameof(DocumentFile.Hash));
            b.Property(x => x.IsSigned).HasColumnName(nameof(DocumentFile.IsSigned));
            b.Property(x => x.UploadedAt).HasColumnName(nameof(DocumentFile.UploadedAt));
            b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<DocumentWorkflowInstance>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentWorkflowInstances", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentWorkflowInstance.TenantId));
            b.Property(x => x.Status).HasColumnName(nameof(DocumentWorkflowInstance.Status)).IsRequired().HasMaxLength(DocumentWorkflowInstanceConsts.StatusMaxLength);
            b.Property(x => x.StartedAt).HasColumnName(nameof(DocumentWorkflowInstance.StartedAt));
            b.Property(x => x.FinishedAt).HasColumnName(nameof(DocumentWorkflowInstance.FinishedAt));
            b.HasOne<Document>().WithMany().IsRequired().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Workflow>().WithMany().IsRequired().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<WorkflowTemplate>().WithMany().IsRequired().HasForeignKey(x => x.WorkflowTemplateId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<WorkflowStepTemplate>().WithMany().IsRequired().HasForeignKey(x => x.CurrentStepId).OnDelete(DeleteBehavior.NoAction);
            b.HasMany(x => x.DocumentWorkflowInstanceFiles).WithOne().HasForeignKey(x => x.DocumentWorkflowInstanceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.DocumentWorkflowInstanceLogss).WithOne().HasForeignKey(x => x.DocumentWorkflowInstanceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DocumentWorkflowInstanceLogs>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentWorkflowInstanceLogss", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentWorkflowInstanceLogs.TenantId));
            b.Property(x => x.Action).HasColumnName(nameof(DocumentWorkflowInstanceLogs.Action)).IsRequired().HasMaxLength(DocumentWorkflowInstanceLogsConsts.ActionMaxLength);
            b.Property(x => x.ActorRole).HasColumnName(nameof(DocumentWorkflowInstanceLogs.ActorRole)).HasMaxLength(DocumentWorkflowInstanceLogsConsts.ActorRoleMaxLength);
            b.Property(x => x.FromStatus).HasColumnName(nameof(DocumentWorkflowInstanceLogs.FromStatus)).HasMaxLength(DocumentWorkflowInstanceLogsConsts.FromStatusMaxLength);
            b.Property(x => x.ToStatus).HasColumnName(nameof(DocumentWorkflowInstanceLogs.ToStatus)).HasMaxLength(DocumentWorkflowInstanceLogsConsts.ToStatusMaxLength);
            b.Property(x => x.Note).HasColumnName(nameof(DocumentWorkflowInstanceLogs.Note));
            b.HasOne<DocumentAssignment>().WithMany().HasForeignKey(x => x.DocumentAssignmentId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<DocumentWorkflowInstance>().WithMany(x => x.DocumentWorkflowInstanceLogss).HasForeignKey(x => x.DocumentWorkflowInstanceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DocumentBackgroundOperation>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "DocumentBackgroundOperations", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(DocumentBackgroundOperation.TenantId));
            b.Property(x => x.UserId).HasColumnName(nameof(DocumentBackgroundOperation.UserId));
            b.Property(x => x.DocumentId).HasColumnName(nameof(DocumentBackgroundOperation.DocumentId));
            b.Property(x => x.OperationType).HasColumnName(nameof(DocumentBackgroundOperation.OperationType)).IsRequired().HasMaxLength(64);
            b.Property(x => x.Status).HasColumnName(nameof(DocumentBackgroundOperation.Status)).IsRequired().HasMaxLength(32);
            b.Property(x => x.Progress).HasColumnName(nameof(DocumentBackgroundOperation.Progress));
            b.Property(x => x.Message).HasColumnName(nameof(DocumentBackgroundOperation.Message)).HasMaxLength(512);
            b.Property(x => x.ErrorMessage).HasColumnName(nameof(DocumentBackgroundOperation.ErrorMessage));
            b.Property(x => x.InputJson).HasColumnName(nameof(DocumentBackgroundOperation.InputJson));
            b.HasIndex(x => new { x.TenantId, x.UserId, x.CreationTime });
            b.HasIndex(x => x.Status);
        });
        builder.Entity<NotificationOutbox>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "NotificationOutboxes", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(NotificationOutbox.TenantId));
            b.Property(x => x.EventType).HasColumnName(nameof(NotificationOutbox.EventType)).IsRequired().HasMaxLength(128);
            b.Property(x => x.PayloadJson).HasColumnName(nameof(NotificationOutbox.PayloadJson)).IsRequired();
            b.Property(x => x.ProcessedTime).HasColumnName(nameof(NotificationOutbox.ProcessedTime));
            b.Property(x => x.RetryCount).HasColumnName(nameof(NotificationOutbox.RetryCount));
            b.Property(x => x.ErrorMessage).HasColumnName(nameof(NotificationOutbox.ErrorMessage));
            b.HasIndex(x => new { x.ProcessedTime, x.CreationTime });
        });
        builder.Entity<UserPushDeviceToken>(b => {
            b.ToTable(HCConsts.DbTablePrefix + "UserPushDeviceTokens", HCConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.TenantId).HasColumnName(nameof(UserPushDeviceToken.TenantId));
            b.Property(x => x.UserId).HasColumnName(nameof(UserPushDeviceToken.UserId));
            b.Property(x => x.FcmToken).HasColumnName(nameof(UserPushDeviceToken.FcmToken)).IsRequired().HasMaxLength(PushDeviceTokenConsts.FcmTokenMaxLength);
            b.Property(x => x.Platform).HasColumnName(nameof(UserPushDeviceToken.Platform)).IsRequired().HasMaxLength(PushDeviceTokenConsts.PlatformMaxLength);
            b.Property(x => x.DeviceId).HasColumnName(nameof(UserPushDeviceToken.DeviceId)).HasMaxLength(PushDeviceTokenConsts.DeviceIdMaxLength);
            b.Property(x => x.IsActive).HasColumnName(nameof(UserPushDeviceToken.IsActive));
            b.Property(x => x.LastSeenTime).HasColumnName(nameof(UserPushDeviceToken.LastSeenTime));
            b.HasIndex(x => new { x.TenantId, x.UserId, x.IsActive });
            b.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId });
            b.HasIndex(x => x.FcmToken);
        });
        builder.ConfigureEventInbox();
        if (builder.IsHostDatabase())
        {
            builder.Entity<Report>(b => {
                b.ToTable(HCConsts.DbTablePrefix + "Reports", HCConsts.DbSchema);
                b.ConfigureByConvention();
                b.Property(x => x.Name).HasColumnName(nameof(Report.Name)).IsRequired().HasMaxLength(ReportConsts.NameMaxLength);
                b.Property(x => x.Url).HasColumnName(nameof(Report.Url)).IsRequired().HasMaxLength(ReportConsts.UrlMaxLength);
                b.Property(x => x.SortOrder).HasColumnName(nameof(Report.SortOrder));
                b.Property(x => x.Image).HasColumnName(nameof(Report.Image)).HasMaxLength(ReportConsts.ImageMaxLength);
            });
        }
    }
}