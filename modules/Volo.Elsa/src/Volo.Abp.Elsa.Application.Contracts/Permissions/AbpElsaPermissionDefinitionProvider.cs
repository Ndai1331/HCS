using Volo.Abp.Elsa.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Volo.Abp.Elsa.Permissions;

public class AbpElsaPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var elsaPermissionGroup = context.AddGroup(AbpElsaPermissions.GroupName, L("Permission:Elsa"));

        // All permissions("*")
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AllPermission, L("Permission:All"), MultiTenancySides.Host);

        // Actions
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Actions.ActionsWorkflowDefinitionsRefresh, L("Permission:actions:workflow-definitions:refresh"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Actions.ActionsWorkflowDefinitionsReload, L("Permission:actions:workflow-definitions:reload"), MultiTenancySides.Host);

        // AiAgents
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiAgents.AiAgentsDelete, L("Permission:ai/agents:delete"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiAgents.AiAgentsWrite, L("Permission:ai/agents:write"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiAgents.AiAgentsRead, L("Permission:ai/agents:read"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiAgents.AiAgentsInvoke, L("Permission:ai/agents:invoke"), MultiTenancySides.Host);

        // AiApiKeys
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiApiKeys.AiApiKeysDelete, L("Permission:ai/api-keys:delete"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiApiKeys.AiApiKeysWrite, L("Permission:ai/api-keys:write"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiApiKeys.AiApiKeysRead, L("Permission:ai/api-keys:read"), MultiTenancySides.Host);

        // AiPlugins
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiPlugins.AiPluginsRead, L("Permission:ai/plugins:read"), MultiTenancySides.Host);

        // AiServices
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiServices.AiServicesWrite, L("Permission:ai/services:write"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiServices.AiServicesDelete, L("Permission:ai/services:delete"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.AiServices.AiServicesRead, L("Permission:ai/services:read"), MultiTenancySides.Host);

        // Cancel
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Cancel.CancelWorkflowInstances, L("Permission:cancel:workflow-instances"), MultiTenancySides.Host);

        // Create
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Create.CreateApplication, L("Permission:create:application"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Create.CreateRole, L("Permission:create:role"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Create.CreateUser, L("Permission:create:user"), MultiTenancySides.Host);

        // Delete
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Delete.DeleteTenants, L("Permission:delete:tenants"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Delete.DeleteWorkflowDefinitions, L("Permission:delete:workflow-definitions"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Delete.DeleteWorkflowInstances, L("Permission:delete:workflow-instances"), MultiTenancySides.Host);

        // Exec
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Exec.ExecWorkflowDefinitions, L("Permission:exec:workflow-definitions"), MultiTenancySides.Host);

        // Execute
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Execute.ExecuteTenantsRefresh, L("Permission:execute:tenants:refresh"), MultiTenancySides.Host);

        // Publish
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Publish.PublishWorkflowDefinitions, L("Permission:publish:workflow-definitions"), MultiTenancySides.Host);

        // Read
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadAll, L("Permission:ReadAll"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadAlterations, L("Permission:read:alterations"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadEnvironments, L("Permission:read:environments"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadSecrets, L("Permission:read:secrets"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadTenants, L("Permission:read:tenants"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadWorkflowContextProviderDescriptors, L("Permission:read:workflow-context-provider-descriptors"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadActivityExecution, L("Permission:read:activity-execution"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadCommitStrategies, L("Permission:read:commit-strategies"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadIncidentStrategies, L("Permission:read:incident-strategies"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadLogPersistenceStrategies, L("Permission:read:log-persistence-strategies"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadWorkflowActivationStrategies, L("Permission:read:workflow-activation-strategies"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadWorkflowDefinitions, L("Permission:read:workflow-definitions"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadWorkflowInstances, L("Permission:read:workflow-instances"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadActivityTypeDefinitions, L("Permission:read:activity-type-definitions"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadResilience, L("Permission:read:resilience"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadResilienceStrategies, L("Permission:read:resilience:strategies"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadResilienceRetries, L("Permission:read:resilience:retries"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadActivityDescriptorsOptions, L("Permission:read:activity-descriptors-options"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadActivityDescriptors, L("Permission:read:activity-descriptors"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadInstalledFeatures, L("Permission:read:installed-features"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadExpressionDescriptors, L("Permission:read:expression-descriptors"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadStorageDrivers, L("Permission:read:storage-drivers"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Read.ReadVariableDescriptors, L("Permission:read:variable-descriptors"), MultiTenancySides.Host);

        // Retract
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Retract.RetractWorkflowDefinitions, L("Permission:retract:workflow-definitions"), MultiTenancySides.Host);

        // Run
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Run.RunAlterations, L("Permission:run:alterations"), MultiTenancySides.Host);

        // Secrets
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Secrets.SecretsDelete, L("Permission:secrets:delete"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Secrets.SecretsWrite, L("Permission:secrets:write"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Secrets.SecretsRead, L("Permission:secrets:read"), MultiTenancySides.Host);

        // Tasks
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Tasks.TasksComplete, L("Permission:tasks:complete"), MultiTenancySides.Host);

        // Trigger
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Trigger.TriggerEvent, L("Permission:trigger:event"), MultiTenancySides.Host);

        // Write
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Write.WriteSecrets, L("Permission:write:secrets"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Write.WriteTenants, L("Permission:write:tenants"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Write.WriteWorkflowDefinitions, L("Permission:write:workflow-definitions"), MultiTenancySides.Host);
        elsaPermissionGroup.AddPermission(AbpElsaPermissions.Write.WriteWorkflowInstances, L("Permission:write:workflow-instances"), MultiTenancySides.Host);
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<AbpElsaResource>(name);
    }
}
