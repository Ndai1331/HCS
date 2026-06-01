using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentHistories;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;
using HC.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace HC.DocumentWorkflowInstances;

public partial class DocumentWorkflowInstancesAppService : DocumentWorkflowInstancesAppServiceBase, IDocumentWorkflowInstancesAppService
{
    private readonly IDocumentSigningQueryService _documentSigningQueryService;
    private readonly IDocumentSigningFilterQueryBuilder _signingFilterQueryBuilder;
    private readonly IWorkflowSubmissionService _workflowSubmissionService;
    private readonly IWorkflowSubmitInfoQueryService _workflowSubmitInfoQueryService;
    private readonly IWorkflowActionService _workflowActionService;
    private readonly IWorkflowInstanceQueryService _workflowInstanceQueryService;
    private readonly IWorkflowSignerManagementService _workflowSignerManagementService;
    private readonly IWorkflowOverdueExtensionService _workflowOverdueExtensionService;
    private readonly IDocumentSigningExportService _documentSigningExportService;

    public DocumentWorkflowInstancesAppService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        DocumentWorkflowInstanceManager documentWorkflowInstanceManager,
        IDistributedCache<DocumentWorkflowInstanceDownloadTokenCacheItem, string> downloadTokenCache,
        IRepository<HC.Documents.Document, Guid> documentRepository,
        IRepository<HC.Workflows.Workflow, Guid> workflowRepository,
        IRepository<HC.WorkflowTemplates.WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IDocumentSigningQueryService documentSigningQueryService,
        IDocumentSigningFilterQueryBuilder signingFilterQueryBuilder,
        IWorkflowSubmissionService workflowSubmissionService,
        IWorkflowSubmitInfoQueryService workflowSubmitInfoQueryService,
        IWorkflowActionService workflowActionService,
        IWorkflowInstanceQueryService workflowInstanceQueryService,
        IWorkflowSignerManagementService workflowSignerManagementService,
        IWorkflowOverdueExtensionService workflowOverdueExtensionService,
        IDocumentSigningExportService documentSigningExportService)
        : base(
            documentWorkflowInstanceRepository,
            documentWorkflowInstanceManager,
            downloadTokenCache,
            documentRepository,
            workflowRepository,
            workflowTemplateRepository,
            workflowStepTemplateRepository)
    {
        _documentSigningQueryService = documentSigningQueryService;
        _signingFilterQueryBuilder = signingFilterQueryBuilder;
        _workflowSubmissionService = workflowSubmissionService;
        _workflowSubmitInfoQueryService = workflowSubmitInfoQueryService;
        _workflowActionService = workflowActionService;
        _workflowInstanceQueryService = workflowInstanceQueryService;
        _workflowSignerManagementService = workflowSignerManagementService;
        _workflowOverdueExtensionService = workflowOverdueExtensionService;
        _documentSigningExportService = documentSigningExportService;
    }

    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<bool> IsDocumentSourceFileWordFormatAsync(Guid documentId)
        => _workflowSubmitInfoQueryService.IsDocumentSourceFileWordFormatAsync(documentId);

    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId)
        => _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(workflowId);

    [UnitOfWork]
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input)
        => _workflowSubmissionService.SubmitToWorkflowAsync(input);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input)
        => _workflowActionService.ProcessWorkflowActionAsync(input);

    [UnitOfWork]
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input)
        => _workflowSubmissionService.ResubmitReturnedWorkflowAsync(input);

    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<ReturnedWorkflowInfoDto> GetReturnedWorkflowInfoAsync(Guid workflowInstanceId)
        => _workflowInstanceQueryService.GetReturnedWorkflowInfoAsync(workflowInstanceId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<List<WorkflowStepStatusDto>> GetAllStepsWithStatusAsync(Guid workflowInstanceId)
        => _workflowInstanceQueryService.GetAllStepsWithStatusAsync(workflowInstanceId);

    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task UpdateWorkflowStepSignersAsync(UpdateWorkflowStepSignersInput input)
        => _workflowSignerManagementService.UpdateWorkflowStepSignersAsync(input);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<DocumentWorkflowStatusDto?> GetActiveWorkflowStatusAsync(Guid documentId)
        => _workflowInstanceQueryService.GetActiveWorkflowStatusAsync(documentId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input)
        => _documentSigningQueryService.GetDocumentSigningListAsync(input);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetWorkflowInstanceLogsAsync(Guid workflowInstanceId)
        => _workflowInstanceQueryService.GetWorkflowInstanceLogsAsync(workflowInstanceId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetWorkflowInstanceFilesAsync(Guid workflowInstanceId)
        => _workflowInstanceQueryService.GetWorkflowInstanceFilesAsync(workflowInstanceId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<List<DocumentHistoryWithNavigationPropertiesDto>> GetDocumentHistoriesByDocumentIdAsync(Guid documentId)
        => _workflowInstanceQueryService.GetDocumentHistoriesByDocumentIdAsync(documentId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<WorkflowOverdueCheckResultDto> CheckAndHandleOverdueAsync(Guid workflowInstanceId)
        => _workflowOverdueExtensionService.CheckAndHandleOverdueAsync(workflowInstanceId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task ExtendWorkflowAsync(ExtendWorkflowInput input)
        => _workflowOverdueExtensionService.ExtendWorkflowAsync(input);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<WorkflowExtensionSummaryDto> GetWorkflowExtensionSummaryAsync(Guid workflowInstanceId)
        => _workflowOverdueExtensionService.GetWorkflowExtensionSummaryAsync(workflowInstanceId);

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public virtual Task<WorkflowInstanceActionBundleDto> GetActionBundleAsync(GetWorkflowInstanceActionBundleInput input)
        => _workflowInstanceQueryService.GetActionBundleAsync(input);
}
