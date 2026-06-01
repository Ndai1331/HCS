using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.WorkflowStepTemplates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

public class WorkflowCommittedStepsQueryService : HCAppService, IWorkflowCommittedStepsQueryService, ITransientDependency
{
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;

    public WorkflowCommittedStepsQueryService(IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository)
    {
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
    }

    public async Task<List<WorkflowStepTemplate>> LoadCommittedWorkflowStepsOrderedAsync(DocumentWorkflowInstance instance)
    {
        var orderedIds = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(instance.CommittedStepTemplateIdsJson);
        if (orderedIds is { Count: > 0 })
        {
            var steps = await _workflowStepTemplateRepository.GetListAsync(x => orderedIds.Contains(x.Id));
            var map = steps.ToDictionary(s => s.Id);
            var ordered = new List<WorkflowStepTemplate>();
            foreach (var id in orderedIds)
            {
                if (map.TryGetValue(id, out var st))
                {
                    ordered.Add(st);
                }
            }

            if (ordered.Count > 0)
            {
                return ordered;
            }
        }

        var legacy = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == instance.WorkflowTemplateId && x.IsActive);
        return legacy.OrderBy(s => s.Order).ToList();
    }
}
