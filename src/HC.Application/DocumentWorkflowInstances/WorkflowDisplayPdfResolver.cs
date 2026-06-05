using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.Documents;
using HC.WorkflowStepTemplates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

public class WorkflowDisplayPdfResolver : IWorkflowDisplayPdfResolver, ITransientDependency
{
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;

    public WorkflowDisplayPdfResolver(
        IRepository<DocumentFile, Guid> documentFileRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<Document, Guid> documentRepository,
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository)
    {
        _documentFileRepository = documentFileRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _documentRepository = documentRepository;
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
    }

    public async Task<WorkflowDisplayPdfFileDto?> ResolveAsync(Guid documentId)
    {
        var effectiveDocumentId = await ResolveEffectiveWorkflowDocumentIdAsync(documentId);
        var assignments = await _documentAssignmentRepository.GetListAsync(x => x.DocumentId == effectiveDocumentId);
        var files = await LoadFilesIncludingAssignmentResultsAsync(effectiveDocumentId, assignments);

        var stepIds = assignments
            .Where(a => a.WorkflowStepTemplateId.HasValue)
            .Select(a => a.WorkflowStepTemplateId!.Value)
            .Distinct()
            .ToList();

        var stepTypeByStepId = new Dictionary<Guid, string>();
        if (stepIds.Count > 0)
        {
            var steps = await _workflowStepTemplateRepository.GetListAsync(x => stepIds.Contains(x.Id));
            foreach (var step in steps)
            {
                stepTypeByStepId[step.Id] = step.Type;
            }
        }

        var selected = WorkflowDisplayPdfSelectionHelper.SelectBestPdf(files, assignments, stepTypeByStepId);
        if (selected == null || string.IsNullOrEmpty(selected.Path))
        {
            return null;
        }

        return new WorkflowDisplayPdfFileDto
        {
            DocumentFileId = selected.Id,
            Name = selected.Name,
            Path = selected.Path,
            IsSigned = selected.IsSigned,
            UploadedAt = selected.UploadedAt
        };
    }

    /// <summary>
    /// When a parent document was submitted for signing, workflow files and assignments live on the child copy.
    /// </summary>
    private async Task<Guid> ResolveEffectiveWorkflowDocumentIdAsync(Guid documentId)
    {
        var document = await _documentRepository.FindAsync(documentId);
        if (document == null || document.SourceType == DocumentSourceType.Workflow)
        {
            return documentId;
        }

        var children = await _documentRepository.GetListAsync(d => d.ParentDocumentId == documentId);
        if (children.Count == 0)
        {
            return documentId;
        }

        var childIds = children.Select(c => c.Id).ToList();
        var activeStatuses = new[]
        {
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.OVERDUE),
            nameof(DocumentWorkflowInstanceStatus.COMPLETED)
        };

        var instances = await _documentWorkflowInstanceRepository.GetListAsync(i =>
            childIds.Contains(i.DocumentId) && activeStatuses.Contains(i.Status));

        if (instances.Count == 0)
        {
            return documentId;
        }

        return instances
            .OrderByDescending(i => i.StartedAt)
            .First()
            .DocumentId;
    }

    /// <summary>
    /// Signed workflow outputs are often stored with DocumentId=null and linked only via DocumentFileResultId.
    /// </summary>
    private async Task<List<DocumentFile>> LoadFilesIncludingAssignmentResultsAsync(
        Guid documentId,
        IReadOnlyList<DocumentAssignment> assignments)
    {
        var files = await _documentFileRepository.GetListAsync(x => x.DocumentId == documentId);
        var fileById = files.ToDictionary(f => f.Id);

        var missingResultFileIds = assignments
            .Where(a => a.DocumentFileResultId.HasValue)
            .Select(a => a.DocumentFileResultId!.Value)
            .Distinct()
            .Where(id => !fileById.ContainsKey(id))
            .ToList();

        if (missingResultFileIds.Count == 0)
        {
            return fileById.Values.ToList();
        }

        var resultFiles = await _documentFileRepository.GetListAsync(x => missingResultFileIds.Contains(x.Id));
        foreach (var resultFile in resultFiles)
        {
            fileById[resultFile.Id] = resultFile;
        }

        return fileById.Values.ToList();
    }
}
