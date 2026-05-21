using System;
using System.Collections.Generic;
using System.Linq;
using HC.Workflows;
using Volo.Abp.DependencyInjection;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowSlaService
{
    DateTime CalculateInitialDeadline(DateTime now, bool isParallel, IEnumerable<int?> stepSlaDays, int? firstStepSlaDays);

    DateTime CalculateStepDeadline(DateTime now, int? slaDays);

    DateTime CalculateExtensionDeadline(DateTime now, DateTime currentFinishedAt, int extensionBusinessDays);
}

public class WorkflowSlaService : IWorkflowSlaService, ITransientDependency
{
    public DateTime CalculateInitialDeadline(DateTime now, bool isParallel, IEnumerable<int?> stepSlaDays, int? firstStepSlaDays)
    {
        if (isParallel)
        {
            var maxSla = stepSlaDays.Where(d => d.HasValue).Select(d => d!.Value).DefaultIfEmpty(0).Max();
            return maxSla > 0 ? BusinessDayCalculator.AddBusinessDays(now, maxSla) : DateTime.MinValue;
        }

        return firstStepSlaDays.HasValue && firstStepSlaDays.Value > 0
            ? BusinessDayCalculator.AddBusinessDays(now, firstStepSlaDays.Value)
            : DateTime.MinValue;
    }

    public DateTime CalculateStepDeadline(DateTime now, int? slaDays)
    {
        return slaDays.HasValue && slaDays.Value > 0
            ? BusinessDayCalculator.AddBusinessDays(now, slaDays.Value)
            : DateTime.MinValue;
    }

    public DateTime CalculateExtensionDeadline(DateTime now, DateTime currentFinishedAt, int extensionBusinessDays)
    {
        var baseInstant = currentFinishedAt > now ? currentFinishedAt : now;
        return BusinessDayCalculator.AddBusinessDays(baseInstant, extensionBusinessDays);
    }
}
