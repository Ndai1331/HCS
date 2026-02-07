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
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using HC.Books;
using HC.Blazor.Pages;

namespace HC.Blazor;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class HCBlazorMappers : MapperBase<BookDto, CreateUpdateBookDto>
{
    public override partial CreateUpdateBookDto Map(BookDto source);
    public override partial void Map(BookDto source, CreateUpdateBookDto destination);
}

[Mapper]
public partial class PositionDtoToPositionUpdateDtoBlazorMapper : MapperBase<PositionDto, PositionUpdateDto>
{
    public override partial PositionUpdateDto Map(PositionDto source);
    public override partial void Map(PositionDto source, PositionUpdateDto destination);
}

[Mapper]
public partial class MasterDataDtoToMasterDataUpdateDtoBlazorMapper : MapperBase<MasterDataDto, MasterDataUpdateDto>
{
    public override partial MasterDataUpdateDto Map(MasterDataDto source);
    public override partial void Map(MasterDataDto source, MasterDataUpdateDto destination);
}

[Mapper]
public partial class WorkflowDefinitionDtoToWorkflowDefinitionUpdateDtoBlazorMapper : MapperBase<WorkflowDefinitionDto, WorkflowDefinitionUpdateDto>
{
    public override partial WorkflowDefinitionUpdateDto Map(WorkflowDefinitionDto source);
    public override partial void Map(WorkflowDefinitionDto source, WorkflowDefinitionUpdateDto destination);
}

[Mapper]
public partial class WorkflowDtoToWorkflowUpdateDtoBlazorMapper : MapperBase<WorkflowDto, WorkflowUpdateDto>
{
    public override partial WorkflowUpdateDto Map(WorkflowDto source);
    public override partial void Map(WorkflowDto source, WorkflowUpdateDto destination);
}

[Mapper]
public partial class WorkflowTemplateDtoToWorkflowTemplateUpdateDtoBlazorMapper : MapperBase<WorkflowTemplateDto, WorkflowTemplateUpdateDto>
{
    public override partial WorkflowTemplateUpdateDto Map(WorkflowTemplateDto source);
    public override partial void Map(WorkflowTemplateDto source, WorkflowTemplateUpdateDto destination);
}

[Mapper]
public partial class WorkflowStepTemplateDtoToWorkflowStepTemplateUpdateDtoBlazorMapper : MapperBase<WorkflowStepTemplateDto, WorkflowStepTemplateUpdateDto>
{
    public override partial WorkflowStepTemplateUpdateDto Map(WorkflowStepTemplateDto source);
    public override partial void Map(WorkflowStepTemplateDto source, WorkflowStepTemplateUpdateDto destination);
}

[Mapper]
public partial class DepartmentDtoToDepartmentUpdateDtoBlazorMapper : MapperBase<DepartmentDto, DepartmentUpdateDto>
{
    public override partial DepartmentUpdateDto Map(DepartmentDto source);
    public override partial void Map(DepartmentDto source, DepartmentUpdateDto destination);
}

public partial class DepartmentTreeViewToDepartmentUpdateDtoBlazorMapper : MapperBase<DepartmentTreeView, DepartmentUpdateDto>
{
    public override DepartmentUpdateDto Map(DepartmentTreeView source)
    {
        if (source == null)
            return null;
        var destination = new DepartmentUpdateDto();
        Map(source, destination);
        return destination;
    }

    public override void Map(DepartmentTreeView source, DepartmentUpdateDto destination)
    {
        if (source == null || destination == null)
            return;
        // Map all properties from DepartmentTreeView (which inherits from DepartmentDto) to DepartmentUpdateDto
        destination.Code = source.Code;
        destination.Name = source.Name;
        destination.ParentId = source.ParentId;
        destination.Level = source.Level;
        destination.SortOrder = source.SortOrder;
        destination.IsActive = source.IsActive;
        destination.LeaderUserId = source.LeaderUserId;
        destination.ConcurrencyStamp = source.ConcurrencyStamp;
    }
}

public partial class DepartmentDtoToDepartmentTreeViewBlazorMapper : MapperBase<DepartmentDto, DepartmentTreeView>
{
    public override DepartmentTreeView Map(DepartmentDto source)
    {
        if (source == null)
            return null;
        var destination = new DepartmentTreeView();
        Map(source, destination);
        return destination;
    }

    public override void Map(DepartmentDto source, DepartmentTreeView destination)
    {
        if (source == null || destination == null)
            return;
        // Map all properties from DepartmentDto (base class)
        destination.Id = source.Id;
        destination.Code = source.Code;
        destination.Name = source.Name;
        destination.ParentId = source.ParentId;
        destination.Level = source.Level;
        destination.SortOrder = source.SortOrder;
        destination.IsActive = source.IsActive;
        destination.LeaderUserId = source.LeaderUserId;
        destination.ConcurrencyStamp = source.ConcurrencyStamp;
        destination.CreationTime = source.CreationTime;
        destination.CreatorId = source.CreatorId;
        destination.LastModificationTime = source.LastModificationTime;
        destination.LastModifierId = source.LastModifierId;
        destination.IsDeleted = source.IsDeleted;
        destination.DeletionTime = source.DeletionTime;
        destination.DeleterId = source.DeleterId;
        // Children will be set separately when building tree
        // Collapsed defaults to true in constructor
    }
}

// Mapping for DepartmentTreeView to DepartmentTreeView (for creating new instance)
public partial class DepartmentTreeViewToDepartmentTreeViewBlazorMapper : MapperBase<DepartmentTreeView, DepartmentTreeView>
{
    public override DepartmentTreeView Map(DepartmentTreeView source)
    {
        if (source == null)
            return null;
        var destination = new DepartmentTreeView();
        Map(source, destination);
        return destination;
    }

    public override void Map(DepartmentTreeView source, DepartmentTreeView destination)
    {
        if (source == null || destination == null)
            return;
        // Map all properties from source to destination
        destination.Id = source.Id;
        destination.Code = source.Code;
        destination.Name = source.Name;
        destination.ParentId = source.ParentId;
        destination.Level = source.Level;
        destination.SortOrder = source.SortOrder;
        destination.IsActive = source.IsActive;
        destination.LeaderUserId = source.LeaderUserId;
        destination.ConcurrencyStamp = source.ConcurrencyStamp;
        destination.CreationTime = source.CreationTime;
        destination.CreatorId = source.CreatorId;
        destination.LastModificationTime = source.LastModificationTime;
        destination.LastModifierId = source.LastModifierId;
        destination.IsDeleted = source.IsDeleted;
        destination.DeletionTime = source.DeletionTime;
        destination.DeleterId = source.DeleterId;
        destination.Children = source.Children;
        destination.Collapsed = source.Collapsed;
    }
}

[Mapper]
public partial class UnitDtoToUnitUpdateDtoBlazorMapper : MapperBase<UnitDto, UnitUpdateDto>
{
    public override partial UnitUpdateDto Map(UnitDto source);
    public override partial void Map(UnitDto source, UnitUpdateDto destination);
}

[Mapper]
public partial class WorkflowStepAssignmentDtoToWorkflowStepAssignmentUpdateDtoBlazorMapper : MapperBase<WorkflowStepAssignmentDto, WorkflowStepAssignmentUpdateDto>
{
    public override partial WorkflowStepAssignmentUpdateDto Map(WorkflowStepAssignmentDto source);
    public override partial void Map(WorkflowStepAssignmentDto source, WorkflowStepAssignmentUpdateDto destination);
}

[Mapper]
public partial class DocumentDtoToDocumentUpdateDtoBlazorMapper : MapperBase<DocumentDto, DocumentUpdateDto>
{
    public override partial DocumentUpdateDto Map(DocumentDto source);
    public override partial void Map(DocumentDto source, DocumentUpdateDto destination);
}

[Mapper]
public partial class DocumentFileDtoToDocumentFileUpdateDtoBlazorMapper : MapperBase<DocumentFileDto, DocumentFileUpdateDto>
{
    public override partial DocumentFileUpdateDto Map(DocumentFileDto source);
    public override partial void Map(DocumentFileDto source, DocumentFileUpdateDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceDtoToDocumentWorkflowInstanceUpdateDtoBlazorMapper : MapperBase<DocumentWorkflowInstanceDto, DocumentWorkflowInstanceUpdateDto>
{
    public override partial DocumentWorkflowInstanceUpdateDto Map(DocumentWorkflowInstanceDto source);
    public override partial void Map(DocumentWorkflowInstanceDto source, DocumentWorkflowInstanceUpdateDto destination);
}

[Mapper]
public partial class DocumentAssignmentDtoToDocumentAssignmentUpdateDtoBlazorMapper : MapperBase<DocumentAssignmentDto, DocumentAssignmentUpdateDto>
{
    public override partial DocumentAssignmentUpdateDto Map(DocumentAssignmentDto source);
    public override partial void Map(DocumentAssignmentDto source, DocumentAssignmentUpdateDto destination);
}

[Mapper]
public partial class DocumentHistoryDtoToDocumentHistoryUpdateDtoBlazorMapper : MapperBase<DocumentHistoryDto, DocumentHistoryUpdateDto>
{
    public override partial DocumentHistoryUpdateDto Map(DocumentHistoryDto source);
    public override partial void Map(DocumentHistoryDto source, DocumentHistoryUpdateDto destination);
}

[Mapper]
public partial class ProjectDtoToProjectUpdateDtoBlazorMapper : MapperBase<ProjectDto, ProjectUpdateDto>
{
    public override partial ProjectUpdateDto Map(ProjectDto source);
    public override partial void Map(ProjectDto source, ProjectUpdateDto destination);
}

[Mapper]
public partial class ProjectMemberDtoToProjectMemberUpdateDtoBlazorMapper : MapperBase<ProjectMemberDto, ProjectMemberUpdateDto>
{
    public override partial ProjectMemberUpdateDto Map(ProjectMemberDto source);
    public override partial void Map(ProjectMemberDto source, ProjectMemberUpdateDto destination);
}

[Mapper]
public partial class ProjectTaskDtoToProjectTaskUpdateDtoBlazorMapper : MapperBase<ProjectTaskDto, ProjectTaskUpdateDto>
{
    public override partial ProjectTaskUpdateDto Map(ProjectTaskDto source);
    public override partial void Map(ProjectTaskDto source, ProjectTaskUpdateDto destination);
}

[Mapper]
public partial class ProjectTaskAssignmentDtoToProjectTaskAssignmentUpdateDtoBlazorMapper : MapperBase<ProjectTaskAssignmentDto, ProjectTaskAssignmentUpdateDto>
{
    public override partial ProjectTaskAssignmentUpdateDto Map(ProjectTaskAssignmentDto source);
    public override partial void Map(ProjectTaskAssignmentDto source, ProjectTaskAssignmentUpdateDto destination);
}

[Mapper]
public partial class ProjectTaskDocumentDtoToProjectTaskDocumentUpdateDtoBlazorMapper : MapperBase<ProjectTaskDocumentDto, ProjectTaskDocumentUpdateDto>
{
    public override partial ProjectTaskDocumentUpdateDto Map(ProjectTaskDocumentDto source);
    public override partial void Map(ProjectTaskDocumentDto source, ProjectTaskDocumentUpdateDto destination);
}

[Mapper]
public partial class NotificationDtoToNotificationUpdateDtoBlazorMapper : MapperBase<NotificationDto, NotificationUpdateDto>
{
    public override partial NotificationUpdateDto Map(NotificationDto source);
    public override partial void Map(NotificationDto source, NotificationUpdateDto destination);
}

[Mapper]
public partial class NotificationReceiverDtoToNotificationReceiverUpdateDtoBlazorMapper : MapperBase<NotificationReceiverDto, NotificationReceiverUpdateDto>
{
    public override partial NotificationReceiverUpdateDto Map(NotificationReceiverDto source);
    public override partial void Map(NotificationReceiverDto source, NotificationReceiverUpdateDto destination);
}

[Mapper]
public partial class SignatureSettingDtoToSignatureSettingUpdateDtoBlazorMapper : MapperBase<SignatureSettingDto, SignatureSettingUpdateDto>
{
    public override partial SignatureSettingUpdateDto Map(SignatureSettingDto source);
    public override partial void Map(SignatureSettingDto source, SignatureSettingUpdateDto destination);
}

[Mapper]
public partial class UserSignatureDtoToUserSignatureUpdateDtoBlazorMapper : MapperBase<UserSignatureDto, UserSignatureUpdateDto>
{
    public override partial UserSignatureUpdateDto Map(UserSignatureDto source);
    public override partial void Map(UserSignatureDto source, UserSignatureUpdateDto destination);
}

[Mapper]
public partial class CalendarEventDtoToCalendarEventUpdateDtoBlazorMapper : MapperBase<CalendarEventDto, CalendarEventUpdateDto>
{
    public override partial CalendarEventUpdateDto Map(CalendarEventDto source);
    public override partial void Map(CalendarEventDto source, CalendarEventUpdateDto destination);
}

[Mapper]
public partial class CalendarEventParticipantDtoToCalendarEventParticipantUpdateDtoBlazorMapper : MapperBase<CalendarEventParticipantDto, CalendarEventParticipantUpdateDto>
{
    public override partial CalendarEventParticipantUpdateDto Map(CalendarEventParticipantDto source);
    public override partial void Map(CalendarEventParticipantDto source, CalendarEventParticipantUpdateDto destination);
}

[Mapper]
public partial class SurveyLocationDtoToSurveyLocationUpdateDtoBlazorMapper : MapperBase<SurveyLocationDto, SurveyLocationUpdateDto>
{
    public override partial SurveyLocationUpdateDto Map(SurveyLocationDto source);
    public override partial void Map(SurveyLocationDto source, SurveyLocationUpdateDto destination);
}

[Mapper]
public partial class SurveyCriteriaDtoToSurveyCriteriaUpdateDtoBlazorMapper : MapperBase<SurveyCriteriaDto, SurveyCriteriaUpdateDto>
{
    public override partial SurveyCriteriaUpdateDto Map(SurveyCriteriaDto source);
    public override partial void Map(SurveyCriteriaDto source, SurveyCriteriaUpdateDto destination);
}

[Mapper]
public partial class SurveySessionDtoToSurveySessionUpdateDtoBlazorMapper : MapperBase<SurveySessionDto, SurveySessionUpdateDto>
{
    public override partial SurveySessionUpdateDto Map(SurveySessionDto source);
    public override partial void Map(SurveySessionDto source, SurveySessionUpdateDto destination);
}

[Mapper]
public partial class SurveyFileDtoToSurveyFileUpdateDtoBlazorMapper : MapperBase<SurveyFileDto, SurveyFileUpdateDto>
{
    public override partial SurveyFileUpdateDto Map(SurveyFileDto source);
    public override partial void Map(SurveyFileDto source, SurveyFileUpdateDto destination);
}

[Mapper]
public partial class SurveyResultDtoToSurveyResultUpdateDtoBlazorMapper : MapperBase<SurveyResultDto, SurveyResultUpdateDto>
{
    public override partial SurveyResultUpdateDto Map(SurveyResultDto source);
    public override partial void Map(SurveyResultDto source, SurveyResultUpdateDto destination);
}

[Mapper]
public partial class UserDepartmentDtoToUserDepartmentUpdateDtoBlazorMapper : MapperBase<UserDepartmentDto, UserDepartmentUpdateDto>
{
    public override partial UserDepartmentUpdateDto Map(UserDepartmentDto source);
    public override partial void Map(UserDepartmentDto source, UserDepartmentUpdateDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceFileDtoToDocumentWorkflowInstanceFileUpdateDtoBlazorMapper : MapperBase<DocumentWorkflowInstanceFileDto, DocumentWorkflowInstanceFileUpdateDto>
{
    public override partial DocumentWorkflowInstanceFileUpdateDto Map(DocumentWorkflowInstanceFileDto source);
    public override partial void Map(DocumentWorkflowInstanceFileDto source, DocumentWorkflowInstanceFileUpdateDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceLogsDtoToDocumentWorkflowInstanceLogsUpdateDtoBlazorMapper : MapperBase<DocumentWorkflowInstanceLogsDto, DocumentWorkflowInstanceLogsUpdateDto>
{
    public override partial DocumentWorkflowInstanceLogsUpdateDto Map(DocumentWorkflowInstanceLogsDto source);
    public override partial void Map(DocumentWorkflowInstanceLogsDto source, DocumentWorkflowInstanceLogsUpdateDto destination);
}