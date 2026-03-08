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
using Volo.Abp.Identity;
using HC.Departments;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.WorkflowDefinitions;
using HC.MasterDatas;
using System;
using HC.Shared;
using HC.Positions;
using System.Linq;
using System.Collections.Generic;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using HC.Books;

namespace HC;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class HCBookToBookDtoMapper : MapperBase<Book, BookDto>
{
    public override partial BookDto Map(Book source);
    public override partial void Map(Book source, BookDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class HCCreateUpdateBookDtoToBookMapper : MapperBase<CreateUpdateBookDto, Book>
{
    public override partial Book Map(CreateUpdateBookDto source);
    public override partial void Map(CreateUpdateBookDto source, Book destination);
}

[Mapper]
public partial class PositionToPositionDtoMappers : MapperBase<Position, PositionDto>
{
    public override partial PositionDto Map(Position source);
    public override partial void Map(Position source, PositionDto destination);
}

[Mapper]
public partial class PositionToPositionExcelDtoMappers : MapperBase<Position, PositionExcelDto>
{
    public override partial PositionExcelDto Map(Position source);
    public override partial void Map(Position source, PositionExcelDto destination);
}

[Mapper]
public partial class MasterDataToMasterDataDtoMappers : MapperBase<MasterData, MasterDataDto>
{
    public override partial MasterDataDto Map(MasterData source);
    public override partial void Map(MasterData source, MasterDataDto destination);
}

[Mapper]
public partial class MasterDataToMasterDataExcelDtoMappers : MapperBase<MasterData, MasterDataExcelDto>
{
    public override partial MasterDataExcelDto Map(MasterData source);
    public override partial void Map(MasterData source, MasterDataExcelDto destination);
}

[Mapper]
public partial class WorkflowDefinitionToWorkflowDefinitionDtoMappers : MapperBase<WorkflowDefinition, WorkflowDefinitionDto>
{
    public override partial WorkflowDefinitionDto Map(WorkflowDefinition source);
    public override partial void Map(WorkflowDefinition source, WorkflowDefinitionDto destination);
}

[Mapper]
public partial class WorkflowDefinitionToWorkflowDefinitionExcelDtoMappers : MapperBase<WorkflowDefinition, WorkflowDefinitionExcelDto>
{
    public override partial WorkflowDefinitionExcelDto Map(WorkflowDefinition source);
    public override partial void Map(WorkflowDefinition source, WorkflowDefinitionExcelDto destination);
}

[Mapper]
public partial class WorkflowToWorkflowDtoMappers : MapperBase<Workflow, WorkflowDto>
{
    public override partial WorkflowDto Map(Workflow source);
    public override partial void Map(Workflow source, WorkflowDto destination);
}

[Mapper]
public partial class WorkflowToWorkflowExcelDtoMappers : MapperBase<Workflow, WorkflowExcelDto>
{
    public override partial WorkflowExcelDto Map(Workflow source);
    public override partial void Map(Workflow source, WorkflowExcelDto destination);
}

[Mapper]
public partial class WorkflowTemplateToWorkflowTemplateDtoMappers : MapperBase<WorkflowTemplate, WorkflowTemplateDto>
{
    public override partial WorkflowTemplateDto Map(WorkflowTemplate source);
    public override partial void Map(WorkflowTemplate source, WorkflowTemplateDto destination);
}

[Mapper]
public partial class WorkflowTemplateToWorkflowTemplateExcelDtoMappers : MapperBase<WorkflowTemplate, WorkflowTemplateExcelDto>
{
    public override partial WorkflowTemplateExcelDto Map(WorkflowTemplate source);
    public override partial void Map(WorkflowTemplate source, WorkflowTemplateExcelDto destination);
}

[Mapper]
public partial class WorkflowTemplateWithNavigationPropertiesToWorkflowTemplateWithNavigationPropertiesDtoMapper : MapperBase<WorkflowTemplateWithNavigationProperties, WorkflowTemplateWithNavigationPropertiesDto>
{
    public override partial WorkflowTemplateWithNavigationPropertiesDto Map(WorkflowTemplateWithNavigationProperties source);
    public override partial void Map(WorkflowTemplateWithNavigationProperties source, WorkflowTemplateWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class PositionToLookupDtoGuidMapper : MapperBase<Position, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Position source);
    public override partial void Map(Position source, LookupDto<Guid> destination);

    public override void AfterMap(Position source, LookupDto<Guid> destination)
    {
        destination.DisplayName = $"{source.Code} - {source.Name}";
    }
}

[Mapper]
public partial class WorkflowToLookupDtoGuidMapper : MapperBase<Workflow, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Workflow source);
    public override partial void Map(Workflow source, LookupDto<Guid> destination);

    public override void AfterMap(Workflow source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class WorkflowStepTemplateToWorkflowStepTemplateDtoMappers : MapperBase<WorkflowStepTemplate, WorkflowStepTemplateDto>
{
    public override partial WorkflowStepTemplateDto Map(WorkflowStepTemplate source);
    public override partial void Map(WorkflowStepTemplate source, WorkflowStepTemplateDto destination);
}

[Mapper]
public partial class WorkflowStepTemplateToWorkflowStepTemplateExcelDtoMappers : MapperBase<WorkflowStepTemplate, WorkflowStepTemplateExcelDto>
{
    public override partial WorkflowStepTemplateExcelDto Map(WorkflowStepTemplate source);
    public override partial void Map(WorkflowStepTemplate source, WorkflowStepTemplateExcelDto destination);
}

[Mapper]
public partial class WorkflowStepTemplateWithNavigationPropertiesToWorkflowStepTemplateWithNavigationPropertiesDtoMapper : MapperBase<WorkflowStepTemplateWithNavigationProperties, WorkflowStepTemplateWithNavigationPropertiesDto>
{
    public override partial WorkflowStepTemplateWithNavigationPropertiesDto Map(WorkflowStepTemplateWithNavigationProperties source);
    public override partial void Map(WorkflowStepTemplateWithNavigationProperties source, WorkflowStepTemplateWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DepartmentToDepartmentDtoMappers : MapperBase<Department, DepartmentDto>
{
    public override partial DepartmentDto Map(Department source);
    public override partial void Map(Department source, DepartmentDto destination);
}

[Mapper]
public partial class DepartmentToDepartmentExcelDtoMappers : MapperBase<Department, DepartmentExcelDto>
{
    public override partial DepartmentExcelDto Map(Department source);
    public override partial void Map(Department source, DepartmentExcelDto destination);
}

[Mapper]
public partial class DepartmentWithNavigationPropertiesToDepartmentWithNavigationPropertiesDtoMapper : MapperBase<DepartmentWithNavigationProperties, DepartmentWithNavigationPropertiesDto>
{
    public override partial DepartmentWithNavigationPropertiesDto Map(DepartmentWithNavigationProperties source);
    public override partial void Map(DepartmentWithNavigationProperties source, DepartmentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class IdentityUserToLookupDtoGuidMapper : MapperBase<IdentityUser, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(IdentityUser source);
    public override partial void Map(IdentityUser source, LookupDto<Guid> destination);

    public override void AfterMap(IdentityUser source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Surname + " " + source.Name;
    }
}

[Mapper]
public partial class UnitToUnitDtoMappers : MapperBase<Unit, UnitDto>
{
    public override partial UnitDto Map(Unit source);
    public override partial void Map(Unit source, UnitDto destination);
}

[Mapper]
public partial class UnitToUnitExcelDtoMappers : MapperBase<Unit, UnitExcelDto>
{
    public override partial UnitExcelDto Map(Unit source);
    public override partial void Map(Unit source, UnitExcelDto destination);
}

[Mapper]
public partial class WorkflowStepAssignmentToWorkflowStepAssignmentDtoMappers : MapperBase<WorkflowStepAssignment, WorkflowStepAssignmentDto>
{
    public override partial WorkflowStepAssignmentDto Map(WorkflowStepAssignment source);
    public override partial void Map(WorkflowStepAssignment source, WorkflowStepAssignmentDto destination);
}

[Mapper]
public partial class WorkflowStepAssignmentToWorkflowStepAssignmentExcelDtoMappers : MapperBase<WorkflowStepAssignment, WorkflowStepAssignmentExcelDto>
{
    public override partial WorkflowStepAssignmentExcelDto Map(WorkflowStepAssignment source);
    public override partial void Map(WorkflowStepAssignment source, WorkflowStepAssignmentExcelDto destination);
}

[Mapper]
public partial class WorkflowStepAssignmentWithNavigationPropertiesToWorkflowStepAssignmentWithNavigationPropertiesDtoMapper : MapperBase<WorkflowStepAssignmentWithNavigationProperties, WorkflowStepAssignmentWithNavigationPropertiesDto>
{
    public override partial WorkflowStepAssignmentWithNavigationPropertiesDto Map(WorkflowStepAssignmentWithNavigationProperties source);
    public override partial void Map(WorkflowStepAssignmentWithNavigationProperties source, WorkflowStepAssignmentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class WorkflowStepTemplateToLookupDtoGuidMapper : MapperBase<WorkflowStepTemplate, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(WorkflowStepTemplate source);
    public override partial void Map(WorkflowStepTemplate source, LookupDto<Guid> destination);

    public override void AfterMap(WorkflowStepTemplate source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class WorkflowTemplateToLookupDtoGuidMapper : MapperBase<WorkflowTemplate, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(WorkflowTemplate source);
    public override partial void Map(WorkflowTemplate source, LookupDto<Guid> destination);

    public override void AfterMap(WorkflowTemplate source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class DocumentToDocumentDtoMappers : MapperBase<Document, DocumentDto>
{
    public override partial DocumentDto Map(Document source);
    public override partial void Map(Document source, DocumentDto destination);
}

[Mapper]
public partial class DocumentToDocumentExcelDtoMappers : MapperBase<Document, DocumentExcelDto>
{
    public override partial DocumentExcelDto Map(Document source);
    public override partial void Map(Document source, DocumentExcelDto destination);
}

[Mapper]
public partial class DocumentWithNavigationPropertiesToDocumentWithNavigationPropertiesDtoMapper : MapperBase<DocumentWithNavigationProperties, DocumentWithNavigationPropertiesDto>
{
    public override partial DocumentWithNavigationPropertiesDto Map(DocumentWithNavigationProperties source);
    public override partial void Map(DocumentWithNavigationProperties source, DocumentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class MasterDataToLookupDtoGuidMapper : MapperBase<MasterData, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(MasterData source);
    public override partial void Map(MasterData source, LookupDto<Guid> destination);

    public override void AfterMap(MasterData source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class UnitToLookupDtoGuidMapper : MapperBase<Unit, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Unit source);
    public override partial void Map(Unit source, LookupDto<Guid> destination);

    public override void AfterMap(Unit source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class DocumentFileToDocumentFileDtoMappers : MapperBase<DocumentFile, DocumentFileDto>
{
    public override partial DocumentFileDto Map(DocumentFile source);
    public override partial void Map(DocumentFile source, DocumentFileDto destination);
}

[Mapper]
public partial class DocumentFileToDocumentFileExcelDtoMappers : MapperBase<DocumentFile, DocumentFileExcelDto>
{
    public override partial DocumentFileExcelDto Map(DocumentFile source);
    public override partial void Map(DocumentFile source, DocumentFileExcelDto destination);
}

[Mapper]
public partial class DocumentFileWithNavigationPropertiesToDocumentFileWithNavigationPropertiesDtoMapper : MapperBase<DocumentFileWithNavigationProperties, DocumentFileWithNavigationPropertiesDto>
{
    public override partial DocumentFileWithNavigationPropertiesDto Map(DocumentFileWithNavigationProperties source);
    public override partial void Map(DocumentFileWithNavigationProperties source, DocumentFileWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DocumentToLookupDtoGuidMapper : MapperBase<Document, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Document source);
    public override partial void Map(Document source, LookupDto<Guid> destination);

    public override void AfterMap(Document source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Title +
        
         (!string.IsNullOrEmpty(source.StorageNumber) ? " - Storage No: " + source.StorageNumber : "") 
         + (!string.IsNullOrEmpty(source.No) ? " - No: " + source.No : "");
    }
}

[Mapper]
public partial class DocumentWorkflowInstanceToDocumentWorkflowInstanceDtoMappers : MapperBase<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>
{
    [MapperIgnoreTarget(nameof(DocumentWorkflowInstanceDto.DocumentWorkflowInstanceFiles))]
    [MapperIgnoreTarget(nameof(DocumentWorkflowInstanceDto.DocumentWorkflowInstanceLogss))]
    public override partial DocumentWorkflowInstanceDto Map(DocumentWorkflowInstance source);
    [MapperIgnoreTarget(nameof(DocumentWorkflowInstanceDto.DocumentWorkflowInstanceFiles))]
    [MapperIgnoreTarget(nameof(DocumentWorkflowInstanceDto.DocumentWorkflowInstanceLogss))]
    public override partial void Map(DocumentWorkflowInstance source, DocumentWorkflowInstanceDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceToDocumentWorkflowInstanceExcelDtoMappers : MapperBase<DocumentWorkflowInstance, DocumentWorkflowInstanceExcelDto>
{
    public override partial DocumentWorkflowInstanceExcelDto Map(DocumentWorkflowInstance source);
    public override partial void Map(DocumentWorkflowInstance source, DocumentWorkflowInstanceExcelDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceWithNavigationPropertiesToDocumentWorkflowInstanceWithNavigationPropertiesDtoMapper : MapperBase<DocumentWorkflowInstanceWithNavigationProperties, DocumentWorkflowInstanceWithNavigationPropertiesDto>
{
    public override partial DocumentWorkflowInstanceWithNavigationPropertiesDto Map(DocumentWorkflowInstanceWithNavigationProperties source);
    public override partial void Map(DocumentWorkflowInstanceWithNavigationProperties source, DocumentWorkflowInstanceWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DocumentAssignmentToDocumentAssignmentDtoMappers : MapperBase<DocumentAssignment, DocumentAssignmentDto>
{
    public override partial DocumentAssignmentDto Map(DocumentAssignment source);
    public override partial void Map(DocumentAssignment source, DocumentAssignmentDto destination);
}

[Mapper]
public partial class DocumentAssignmentToDocumentAssignmentExcelDtoMappers : MapperBase<DocumentAssignment, DocumentAssignmentExcelDto>
{
    public override partial DocumentAssignmentExcelDto Map(DocumentAssignment source);
    public override partial void Map(DocumentAssignment source, DocumentAssignmentExcelDto destination);
}

[Mapper]
public partial class DocumentAssignmentWithNavigationPropertiesToDocumentAssignmentWithNavigationPropertiesDtoMapper : MapperBase<DocumentAssignmentWithNavigationProperties, DocumentAssignmentWithNavigationPropertiesDto>
{
    public override partial DocumentAssignmentWithNavigationPropertiesDto Map(DocumentAssignmentWithNavigationProperties source);
    public override partial void Map(DocumentAssignmentWithNavigationProperties source, DocumentAssignmentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DocumentHistoryToDocumentHistoryDtoMappers : MapperBase<DocumentHistory, DocumentHistoryDto>
{
    public override partial DocumentHistoryDto Map(DocumentHistory source);
    public override partial void Map(DocumentHistory source, DocumentHistoryDto destination);
}

[Mapper]
public partial class DocumentHistoryToDocumentHistoryExcelDtoMappers : MapperBase<DocumentHistory, DocumentHistoryExcelDto>
{
    public override partial DocumentHistoryExcelDto Map(DocumentHistory source);
    public override partial void Map(DocumentHistory source, DocumentHistoryExcelDto destination);
}

[Mapper]
public partial class DocumentHistoryWithNavigationPropertiesToDocumentHistoryWithNavigationPropertiesDtoMapper : MapperBase<DocumentHistoryWithNavigationProperties, DocumentHistoryWithNavigationPropertiesDto>
{
    public override partial DocumentHistoryWithNavigationPropertiesDto Map(DocumentHistoryWithNavigationProperties source);
    public override partial void Map(DocumentHistoryWithNavigationProperties source, DocumentHistoryWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class ProjectToProjectDtoMappers : MapperBase<Project, ProjectDto>
{
    public override partial ProjectDto Map(Project source);
    public override partial void Map(Project source, ProjectDto destination);

    public override void AfterMap(Project source, ProjectDto destination)
    {
        // Convert string Status to enum
        if (!string.IsNullOrWhiteSpace(source.Status) && Enum.TryParse<ProjectStatus>(source.Status, out var statusEnum))
        {
            destination.Status = statusEnum;
        }
        else
        {
            destination.Status = ProjectStatus.PLANNING;
            // Default value
        }
    }
}

[Mapper]
public partial class ProjectToProjectExcelDtoMappers : MapperBase<Project, ProjectExcelDto>
{
    public override partial ProjectExcelDto Map(Project source);
    public override partial void Map(Project source, ProjectExcelDto destination);
}

[Mapper]
public partial class ProjectWithNavigationPropertiesToProjectWithNavigationPropertiesDtoMapper : MapperBase<ProjectWithNavigationProperties, ProjectWithNavigationPropertiesDto>
{
    public override partial ProjectWithNavigationPropertiesDto Map(ProjectWithNavigationProperties source);
    public override partial void Map(ProjectWithNavigationProperties source, ProjectWithNavigationPropertiesDto destination);

    public override void AfterMap(ProjectWithNavigationProperties source, ProjectWithNavigationPropertiesDto destination)
    {
        // Convert string Status to enum in nested ProjectDto
        if (destination.Project != null && !string.IsNullOrWhiteSpace(source.Project.Status))
        {
            if (Enum.TryParse<ProjectStatus>(source.Project.Status, out var statusEnum))
            {
                destination.Project.Status = statusEnum;
            }
            else
            {
                destination.Project.Status = ProjectStatus.PLANNING;
                // Default value
            }
        }
    }
}

[Mapper]
public partial class DepartmentToLookupDtoGuidMapper : MapperBase<Department, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Department source);
    public override partial void Map(Department source, LookupDto<Guid> destination);

    public override void AfterMap(Department source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class ProjectMemberToProjectMemberDtoMappers : MapperBase<ProjectMember, ProjectMemberDto>
{
    public override partial ProjectMemberDto Map(ProjectMember source);
    public override partial void Map(ProjectMember source, ProjectMemberDto destination);

    public override void AfterMap(ProjectMember source, ProjectMemberDto destination)
    {
        // Convert string MemberRole to enum for DTO
        if (!string.IsNullOrWhiteSpace(source.MemberRole) && Enum.TryParse<ProjectMemberRole>(source.MemberRole, out var role))
        {
            destination.MemberRole = role;
        }
        else
        {
            destination.MemberRole = ProjectMemberRole.MEMBER;
        }
    }
}

[Mapper]
public partial class ProjectMemberToProjectMemberExcelDtoMappers : MapperBase<ProjectMember, ProjectMemberExcelDto>
{
    public override partial ProjectMemberExcelDto Map(ProjectMember source);
    public override partial void Map(ProjectMember source, ProjectMemberExcelDto destination);
}

[Mapper]
public partial class ProjectMemberWithNavigationPropertiesToProjectMemberWithNavigationPropertiesDtoMapper : MapperBase<ProjectMemberWithNavigationProperties, ProjectMemberWithNavigationPropertiesDto>
{
    public override partial ProjectMemberWithNavigationPropertiesDto Map(ProjectMemberWithNavigationProperties source);
    public override partial void Map(ProjectMemberWithNavigationProperties source, ProjectMemberWithNavigationPropertiesDto destination);

    public override void AfterMap(ProjectMemberWithNavigationProperties source, ProjectMemberWithNavigationPropertiesDto destination)
    {
        // Handle null Project when it's been deleted
        if (source.Project == null)
        {
            destination.Project = null;
        }
    }
}

[Mapper]
public partial class ProjectToLookupDtoGuidMapper : MapperBase<Project, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Project source);
    public override partial void Map(Project source, LookupDto<Guid> destination);

    public override void AfterMap(Project source, LookupDto<Guid> destination)
    {
        destination.DisplayName = $"{source.Code} - {source.Name}";
    }
}

[Mapper]
public partial class ProjectTaskToProjectTaskDtoMappers : MapperBase<ProjectTask, ProjectTaskDto>
{
    public override partial ProjectTaskDto Map(ProjectTask source);
    public override partial void Map(ProjectTask source, ProjectTaskDto destination);
}

[Mapper]
public partial class ProjectTaskToProjectTaskExcelDtoMappers : MapperBase<ProjectTask, ProjectTaskExcelDto>
{
    public override partial ProjectTaskExcelDto Map(ProjectTask source);
    public override partial void Map(ProjectTask source, ProjectTaskExcelDto destination);
}

[Mapper]
public partial class ProjectTaskWithNavigationPropertiesToProjectTaskWithNavigationPropertiesDtoMapper : MapperBase<ProjectTaskWithNavigationProperties, ProjectTaskWithNavigationPropertiesDto>
{
    public override partial ProjectTaskWithNavigationPropertiesDto Map(ProjectTaskWithNavigationProperties source);
    public override partial void Map(ProjectTaskWithNavigationProperties source, ProjectTaskWithNavigationPropertiesDto destination);

    public override void AfterMap(ProjectTaskWithNavigationProperties source, ProjectTaskWithNavigationPropertiesDto destination)
    {
        // Handle null Project when it's been deleted
        if (source.Project == null)
        {
            destination.Project = null;
        }
    }
}

[Mapper]
public partial class ProjectTaskAssignmentToProjectTaskAssignmentDtoMappers : MapperBase<ProjectTaskAssignment, ProjectTaskAssignmentDto>
{
    public override partial ProjectTaskAssignmentDto Map(ProjectTaskAssignment source);
    public override partial void Map(ProjectTaskAssignment source, ProjectTaskAssignmentDto destination);
}

[Mapper]
public partial class ProjectTaskAssignmentToProjectTaskAssignmentExcelDtoMappers : MapperBase<ProjectTaskAssignment, ProjectTaskAssignmentExcelDto>
{
    public override partial ProjectTaskAssignmentExcelDto Map(ProjectTaskAssignment source);
    public override partial void Map(ProjectTaskAssignment source, ProjectTaskAssignmentExcelDto destination);
}

[Mapper]
public partial class ProjectTaskAssignmentWithNavigationPropertiesToProjectTaskAssignmentWithNavigationPropertiesDtoMapper : MapperBase<ProjectTaskAssignmentWithNavigationProperties, ProjectTaskAssignmentWithNavigationPropertiesDto>
{
    public override partial ProjectTaskAssignmentWithNavigationPropertiesDto Map(ProjectTaskAssignmentWithNavigationProperties source);
    public override partial void Map(ProjectTaskAssignmentWithNavigationProperties source, ProjectTaskAssignmentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class ProjectTaskToLookupDtoGuidMapper : MapperBase<ProjectTask, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(ProjectTask source);
    public override partial void Map(ProjectTask source, LookupDto<Guid> destination);

    public override void AfterMap(ProjectTask source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Title;
    }
}

[Mapper]
public partial class ProjectTaskDocumentToProjectTaskDocumentDtoMappers : MapperBase<ProjectTaskDocument, ProjectTaskDocumentDto>
{
    public override partial ProjectTaskDocumentDto Map(ProjectTaskDocument source);
    public override partial void Map(ProjectTaskDocument source, ProjectTaskDocumentDto destination);
}

[Mapper]
public partial class ProjectTaskDocumentToProjectTaskDocumentExcelDtoMappers : MapperBase<ProjectTaskDocument, ProjectTaskDocumentExcelDto>
{
    public override partial ProjectTaskDocumentExcelDto Map(ProjectTaskDocument source);
    public override partial void Map(ProjectTaskDocument source, ProjectTaskDocumentExcelDto destination);
}

[Mapper]
public partial class ProjectTaskDocumentWithNavigationPropertiesToProjectTaskDocumentWithNavigationPropertiesDtoMapper : MapperBase<ProjectTaskDocumentWithNavigationProperties, ProjectTaskDocumentWithNavigationPropertiesDto>
{
    public override partial ProjectTaskDocumentWithNavigationPropertiesDto Map(ProjectTaskDocumentWithNavigationProperties source);
    public override partial void Map(ProjectTaskDocumentWithNavigationProperties source, ProjectTaskDocumentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class NotificationToNotificationDtoMappers : MapperBase<Notification, NotificationDto>
{
    public override partial NotificationDto Map(Notification source);
    public override partial void Map(Notification source, NotificationDto destination);
}

[Mapper]
public partial class NotificationToNotificationExcelDtoMappers : MapperBase<Notification, NotificationExcelDto>
{
    public override partial NotificationExcelDto Map(Notification source);
    public override partial void Map(Notification source, NotificationExcelDto destination);
}

[Mapper]
public partial class NotificationReceiverToNotificationReceiverDtoMappers : MapperBase<NotificationReceiver, NotificationReceiverDto>
{
    public override partial NotificationReceiverDto Map(NotificationReceiver source);
    public override partial void Map(NotificationReceiver source, NotificationReceiverDto destination);
}

[Mapper]
public partial class NotificationReceiverToNotificationReceiverExcelDtoMappers : MapperBase<NotificationReceiver, NotificationReceiverExcelDto>
{
    public override partial NotificationReceiverExcelDto Map(NotificationReceiver source);
    public override partial void Map(NotificationReceiver source, NotificationReceiverExcelDto destination);
}

[Mapper]
public partial class NotificationReceiverWithNavigationPropertiesToNotificationReceiverWithNavigationPropertiesDtoMapper : MapperBase<NotificationReceiverWithNavigationProperties, NotificationReceiverWithNavigationPropertiesDto>
{
    public override partial NotificationReceiverWithNavigationPropertiesDto Map(NotificationReceiverWithNavigationProperties source);
    public override partial void Map(NotificationReceiverWithNavigationProperties source, NotificationReceiverWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class NotificationToLookupDtoGuidMapper : MapperBase<Notification, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(Notification source);
    public override partial void Map(Notification source, LookupDto<Guid> destination);

    public override void AfterMap(Notification source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Title;
    }
}

[Mapper]
public partial class SignatureSettingToSignatureSettingDtoMappers : MapperBase<SignatureSetting, SignatureSettingDto>
{
    public override partial SignatureSettingDto Map(SignatureSetting source);
    public override partial void Map(SignatureSetting source, SignatureSettingDto destination);
}

[Mapper]
public partial class SignatureSettingToSignatureSettingExcelDtoMappers : MapperBase<SignatureSetting, SignatureSettingExcelDto>
{
    public override partial SignatureSettingExcelDto Map(SignatureSetting source);
    public override partial void Map(SignatureSetting source, SignatureSettingExcelDto destination);
}

[Mapper]
public partial class UserSignatureToUserSignatureDtoMappers : MapperBase<UserSignature, UserSignatureDto>
{
    public override partial UserSignatureDto Map(UserSignature source);
    public override partial void Map(UserSignature source, UserSignatureDto destination);
}

[Mapper]
public partial class UserSignatureToUserSignatureExcelDtoMappers : MapperBase<UserSignature, UserSignatureExcelDto>
{
    public override partial UserSignatureExcelDto Map(UserSignature source);
    public override partial void Map(UserSignature source, UserSignatureExcelDto destination);
}

[Mapper]
public partial class UserSignatureWithNavigationPropertiesToUserSignatureWithNavigationPropertiesDtoMapper : MapperBase<UserSignatureWithNavigationProperties, UserSignatureWithNavigationPropertiesDto>
{
    public override partial UserSignatureWithNavigationPropertiesDto Map(UserSignatureWithNavigationProperties source);
    public override partial void Map(UserSignatureWithNavigationProperties source, UserSignatureWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class CalendarEventToCalendarEventDtoMappers : MapperBase<CalendarEvent, CalendarEventDto>
{
    public override partial CalendarEventDto Map(CalendarEvent source);
    public override partial void Map(CalendarEvent source, CalendarEventDto destination);
}

[Mapper]
public partial class CalendarEventToCalendarEventExcelDtoMappers : MapperBase<CalendarEvent, CalendarEventExcelDto>
{
    public override partial CalendarEventExcelDto Map(CalendarEvent source);
    public override partial void Map(CalendarEvent source, CalendarEventExcelDto destination);
}

[Mapper]
public partial class CalendarEventParticipantToCalendarEventParticipantDtoMappers : MapperBase<CalendarEventParticipant, CalendarEventParticipantDto>
{
    public override partial CalendarEventParticipantDto Map(CalendarEventParticipant source);
    public override partial void Map(CalendarEventParticipant source, CalendarEventParticipantDto destination);
}

[Mapper]
public partial class CalendarEventParticipantToCalendarEventParticipantExcelDtoMappers : MapperBase<CalendarEventParticipant, CalendarEventParticipantExcelDto>
{
    public override partial CalendarEventParticipantExcelDto Map(CalendarEventParticipant source);
    public override partial void Map(CalendarEventParticipant source, CalendarEventParticipantExcelDto destination);
}

[Mapper]
public partial class CalendarEventParticipantWithNavigationPropertiesToCalendarEventParticipantWithNavigationPropertiesDtoMapper : MapperBase<CalendarEventParticipantWithNavigationProperties, CalendarEventParticipantWithNavigationPropertiesDto>
{
    public override partial CalendarEventParticipantWithNavigationPropertiesDto Map(CalendarEventParticipantWithNavigationProperties source);
    public override partial void Map(CalendarEventParticipantWithNavigationProperties source, CalendarEventParticipantWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class CalendarEventToLookupDtoGuidMapper : MapperBase<CalendarEvent, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(CalendarEvent source);
    public override partial void Map(CalendarEvent source, LookupDto<Guid> destination);

    public override void AfterMap(CalendarEvent source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Title;
    }
}

[Mapper]
public partial class SurveyLocationToSurveyLocationDtoMappers : MapperBase<SurveyLocation, SurveyLocationDto>
{
    public override partial SurveyLocationDto Map(SurveyLocation source);
    public override partial void Map(SurveyLocation source, SurveyLocationDto destination);
}

[Mapper]
public partial class SurveyLocationToSurveyLocationExcelDtoMappers : MapperBase<SurveyLocation, SurveyLocationExcelDto>
{
    public override partial SurveyLocationExcelDto Map(SurveyLocation source);
    public override partial void Map(SurveyLocation source, SurveyLocationExcelDto destination);
}

[Mapper]
public partial class SurveyCriteriaToSurveyCriteriaDtoMappers : MapperBase<SurveyCriteria, SurveyCriteriaDto>
{
    public override partial SurveyCriteriaDto Map(SurveyCriteria source);
    public override partial void Map(SurveyCriteria source, SurveyCriteriaDto destination);
}

[Mapper]
public partial class SurveyCriteriaToSurveyCriteriaExcelDtoMappers : MapperBase<SurveyCriteria, SurveyCriteriaExcelDto>
{
    public override partial SurveyCriteriaExcelDto Map(SurveyCriteria source);
    public override partial void Map(SurveyCriteria source, SurveyCriteriaExcelDto destination);
}

[Mapper]
public partial class SurveyCriteriaWithNavigationPropertiesToSurveyCriteriaWithNavigationPropertiesDtoMapper : MapperBase<SurveyCriteriaWithNavigationProperties, SurveyCriteriaWithNavigationPropertiesDto>
{
    public override partial SurveyCriteriaWithNavigationPropertiesDto Map(SurveyCriteriaWithNavigationProperties source);
    public override partial void Map(SurveyCriteriaWithNavigationProperties source, SurveyCriteriaWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class SurveyLocationToLookupDtoGuidMapper : MapperBase<SurveyLocation, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(SurveyLocation source);
    public override partial void Map(SurveyLocation source, LookupDto<Guid> destination);

    public override void AfterMap(SurveyLocation source, LookupDto<Guid> destination)
    {
        destination.DisplayName = $"{source.Code} - {source.Name}";
    }
}

[Mapper]
public partial class SurveySessionToSurveySessionDtoMappers : MapperBase<SurveySession, SurveySessionDto>
{
    public override partial SurveySessionDto Map(SurveySession source);
    public override partial void Map(SurveySession source, SurveySessionDto destination);
}

[Mapper]
public partial class SurveySessionToSurveySessionExcelDtoMappers : MapperBase<SurveySession, SurveySessionExcelDto>
{
    public override partial SurveySessionExcelDto Map(SurveySession source);
    public override partial void Map(SurveySession source, SurveySessionExcelDto destination);
}

[Mapper]
public partial class SurveySessionWithNavigationPropertiesToSurveySessionWithNavigationPropertiesDtoMapper : MapperBase<SurveySessionWithNavigationProperties, SurveySessionWithNavigationPropertiesDto>
{
    public override partial SurveySessionWithNavigationPropertiesDto Map(SurveySessionWithNavigationProperties source);
    public override partial void Map(SurveySessionWithNavigationProperties source, SurveySessionWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class SurveyFileToSurveyFileDtoMappers : MapperBase<SurveyFile, SurveyFileDto>
{
    public override partial SurveyFileDto Map(SurveyFile source);
    public override partial void Map(SurveyFile source, SurveyFileDto destination);
}

[Mapper]
public partial class SurveyFileToSurveyFileExcelDtoMappers : MapperBase<SurveyFile, SurveyFileExcelDto>
{
    public override partial SurveyFileExcelDto Map(SurveyFile source);
    public override partial void Map(SurveyFile source, SurveyFileExcelDto destination);
}

[Mapper]
public partial class SurveyFileWithNavigationPropertiesToSurveyFileWithNavigationPropertiesDtoMapper : MapperBase<SurveyFileWithNavigationProperties, SurveyFileWithNavigationPropertiesDto>
{
    public override partial SurveyFileWithNavigationPropertiesDto Map(SurveyFileWithNavigationProperties source);
    public override partial void Map(SurveyFileWithNavigationProperties source, SurveyFileWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class SurveySessionToLookupDtoGuidMapper : MapperBase<SurveySession, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(SurveySession source);
    public override partial void Map(SurveySession source, LookupDto<Guid> destination);

    public override void AfterMap(SurveySession source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.SessionDisplay;
    }
}

[Mapper]
public partial class SurveyResultToSurveyResultDtoMappers : MapperBase<SurveyResult, SurveyResultDto>
{
    public override partial SurveyResultDto Map(SurveyResult source);
    public override partial void Map(SurveyResult source, SurveyResultDto destination);
}

[Mapper]
public partial class SurveyResultToSurveyResultExcelDtoMappers : MapperBase<SurveyResult, SurveyResultExcelDto>
{
    public override partial SurveyResultExcelDto Map(SurveyResult source);
    public override partial void Map(SurveyResult source, SurveyResultExcelDto destination);
}

[Mapper]
public partial class SurveyResultWithNavigationPropertiesToSurveyResultWithNavigationPropertiesDtoMapper : MapperBase<SurveyResultWithNavigationProperties, SurveyResultWithNavigationPropertiesDto>
{
    public override partial SurveyResultWithNavigationPropertiesDto Map(SurveyResultWithNavigationProperties source);
    public override partial void Map(SurveyResultWithNavigationProperties source, SurveyResultWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class SurveyCriteriaToLookupDtoGuidMapper : MapperBase<SurveyCriteria, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(SurveyCriteria source);
    public override partial void Map(SurveyCriteria source, LookupDto<Guid> destination);

    public override void AfterMap(SurveyCriteria source, LookupDto<Guid> destination)
    {
        destination.DisplayName = $"{source.Code} - {source.Name}";
    }
}

[Mapper]
public partial class WorkflowWithNavigationPropertiesToWorkflowWithNavigationPropertiesDtoMapper : MapperBase<WorkflowWithNavigationProperties, WorkflowWithNavigationPropertiesDto>
{
    public override partial WorkflowWithNavigationPropertiesDto Map(WorkflowWithNavigationProperties source);
    public override partial void Map(WorkflowWithNavigationProperties source, WorkflowWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class WorkflowDefinitionToLookupDtoGuidMapper : MapperBase<WorkflowDefinition, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(WorkflowDefinition source);
    public override partial void Map(WorkflowDefinition source, LookupDto<Guid> destination);

    public override void AfterMap(WorkflowDefinition source, LookupDto<Guid> destination)
    {
        destination.DisplayName = $"{source.Code} - {source.Name}";
    }
}

[Mapper]
public partial class UserDepartmentToUserDepartmentDtoMappers : MapperBase<UserDepartment, UserDepartmentDto>
{
    public override partial UserDepartmentDto Map(UserDepartment source);
    public override partial void Map(UserDepartment source, UserDepartmentDto destination);
}

[Mapper]
public partial class UserDepartmentToUserDepartmentExcelDtoMappers : MapperBase<UserDepartment, UserDepartmentExcelDto>
{
    public override partial UserDepartmentExcelDto Map(UserDepartment source);
    public override partial void Map(UserDepartment source, UserDepartmentExcelDto destination);
}

[Mapper]
public partial class UserDepartmentWithNavigationPropertiesToUserDepartmentWithNavigationPropertiesDtoMapper : MapperBase<UserDepartmentWithNavigationProperties, UserDepartmentWithNavigationPropertiesDto>
{
    public override partial UserDepartmentWithNavigationPropertiesDto Map(UserDepartmentWithNavigationProperties source);
    public override partial void Map(UserDepartmentWithNavigationProperties source, UserDepartmentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DocumentFileToLookupDtoGuidMapper : MapperBase<DocumentFile, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(DocumentFile source);
    public override partial void Map(DocumentFile source, LookupDto<Guid> destination);

    public override void AfterMap(DocumentFile source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class DocumentWorkflowInstanceFileToDocumentWorkflowInstanceFileDtoMappers : MapperBase<DocumentWorkflowInstanceFile, DocumentWorkflowInstanceFileDto>
{
    public override partial DocumentWorkflowInstanceFileDto Map(DocumentWorkflowInstanceFile source);
    public override partial void Map(DocumentWorkflowInstanceFile source, DocumentWorkflowInstanceFileDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceFileWithNavigationPropertiesToDocumentWorkflowInstanceFileWithNavigationPropertiesDtoMapper : MapperBase<DocumentWorkflowInstanceFileWithNavigationProperties, DocumentWorkflowInstanceFileWithNavigationPropertiesDto>
{
    public override partial DocumentWorkflowInstanceFileWithNavigationPropertiesDto Map(DocumentWorkflowInstanceFileWithNavigationProperties source);
    public override partial void Map(DocumentWorkflowInstanceFileWithNavigationProperties source, DocumentWorkflowInstanceFileWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceLogsToDocumentWorkflowInstanceLogsDtoMappers : MapperBase<DocumentWorkflowInstanceLogs, DocumentWorkflowInstanceLogsDto>
{
    public override partial DocumentWorkflowInstanceLogsDto Map(DocumentWorkflowInstanceLogs source);
    public override partial void Map(DocumentWorkflowInstanceLogs source, DocumentWorkflowInstanceLogsDto destination);
}

[Mapper]
public partial class DocumentWorkflowInstanceLogsWithNavigationPropertiesToDocumentWorkflowInstanceLogsWithNavigationPropertiesDtoMapper : MapperBase<DocumentWorkflowInstanceLogsWithNavigationProperties, DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>
{
    public override partial DocumentWorkflowInstanceLogsWithNavigationPropertiesDto Map(DocumentWorkflowInstanceLogsWithNavigationProperties source);
    public override partial void Map(DocumentWorkflowInstanceLogsWithNavigationProperties source, DocumentWorkflowInstanceLogsWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DocumentAssignmentToLookupDtoGuidMapper : MapperBase<DocumentAssignment, LookupDto<Guid>>
{
    public override partial LookupDto<Guid> Map(DocumentAssignment source);
    public override partial void Map(DocumentAssignment source, LookupDto<Guid> destination);

    public override void AfterMap(DocumentAssignment source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.ActionType;
    }
}