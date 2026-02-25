using System.Linq;
using Volo.Abp.Features;
using Volo.Abp.Validation.StringValues;
using Volo.Forms;

namespace HC.Features;

/// <summary>
/// Ensures Forms feature definition exists in host runtime.
/// This prevents startup/runtime failures when permissions require
/// Volo.Forms.Enable but module feature definitions are not loaded.
/// </summary>
public class FormsFeatureFallbackDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.GetGroupOrNull(FormsFeatures.GroupName) ??
                    context.AddGroup(FormsFeatures.GroupName);

        if (group.Features.FirstOrDefault(f => f.Name == FormsFeatures.Enable) == null)
        {
            group.AddFeature(
                FormsFeatures.Enable,
                "true",
                valueType: new ToggleStringValueType()
            );
        }
    }
}
