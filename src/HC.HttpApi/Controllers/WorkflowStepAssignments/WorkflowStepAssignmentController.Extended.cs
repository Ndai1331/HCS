using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.Shared;
using HC.WorkflowStepAssignments;

namespace HC.Controllers.WorkflowStepAssignments;

[RemoteService]
[Area("app")]
[ControllerName("WorkflowStepAssignment")]
[Route("api/app/workflow-step-assignments")]
public class WorkflowStepAssignmentController : WorkflowStepAssignmentControllerBase, IWorkflowStepAssignmentsAppService
{
    public WorkflowStepAssignmentController(IWorkflowStepAssignmentsAppService workflowStepAssignmentsAppService) : base(workflowStepAssignmentsAppService)
    {
    }

    [HttpGet]
    [Route("identity-role-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityRoleLookupAsync(LookupRequestDto input)
    {
        return _workflowStepAssignmentsAppService.GetIdentityRoleLookupAsync(input);
    }
}