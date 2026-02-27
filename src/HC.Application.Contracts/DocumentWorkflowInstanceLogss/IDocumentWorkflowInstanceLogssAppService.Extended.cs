using System;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstanceLogss;

public partial interface IDocumentWorkflowInstanceLogssAppService
{
    Task<WorkflowChartStatisticsDto> GetWorkflowChartStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
}