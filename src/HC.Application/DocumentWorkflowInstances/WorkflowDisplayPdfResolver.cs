using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.WorkflowStepTemplates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

public class WorkflowDisplayPdfResolver : IWorkflowDisplayPdfResolver, ITransientDependency
{
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;

    public WorkflowDisplayPdfResolver(
        IRepository<DocumentFile, Guid> documentFileRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository)
    {
        _documentFileRepository = documentFileRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
    }

    public async Task<WorkflowDisplayPdfFileDto?> ResolveAsync(Guid documentId)
    {
        var files = await _documentFileRepository.GetListAsync(x => x.DocumentId == documentId);
        var assignments = await _documentAssignmentRepository.GetListAsync(x => x.DocumentId == documentId);

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
}
