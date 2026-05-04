using Volo.Abp.Reflection;

namespace Volo.Abp.Elsa.Permissions;

public class AbpElsaPermissions
{
    public const string GroupName = "Elsa";

    public const string AllPermission = "*";

    public static class Actions
    {
        public const string ActionsWorkflowDefinitionsRefresh = "actions:workflow-definitions:refresh";
        public const string ActionsWorkflowDefinitionsReload = "actions:workflow-definitions:reload";
    }

    public static class AiAgents
    {
        public const string AiAgentsDelete = "ai/agents:delete";
        public const string AiAgentsWrite = "ai/agents:write";
        public const string AiAgentsRead = "ai/agents:read";
        public const string AiAgentsInvoke = "ai/agents:invoke";
    }

    public static class AiApiKeys
    {
        public const string AiApiKeysDelete = "ai/api-keys:delete";
        public const string AiApiKeysWrite = "ai/api-keys:write";
        public const string AiApiKeysRead = "ai/api-keys:read";
    }

    public static class AiPlugins
    {
        public const string AiPluginsRead = "ai/plugins:read";
    }

    public static class AiServices
    {
        public const string AiServicesWrite = "ai/services:write";
        public const string AiServicesDelete = "ai/services:delete";
        public const string AiServicesRead = "ai/services:read";
    }

    public static class Cancel
    {
        public const string CancelWorkflowInstances = "cancel:workflow-instances";
    }

    public static class Create
    {
        public const string CreateApplication = "create:application";
        public const string CreateRole = "create:role";
        public const string CreateUser = "create:user";
    }

    public static class Delete
    {
        public const string DeleteTenants = "delete:tenants";
        public const string DeleteWorkflowDefinitions = "delete:workflow-definitions";
        public const string DeleteWorkflowInstances = "delete:workflow-instances";
    }

    public static class Exec
    {
        public const string ExecWorkflowDefinitions = "exec:workflow-definitions";
    }

    public static class Execute
    {
        public const string ExecuteTenantsRefresh = "execute:tenants:refresh";
    }

    public static class Publish
    {
        public const string PublishWorkflowDefinitions = "publish:workflow-definitions";
    }

    public static class Read
    {
        public const string ReadAll = "read:*";
        public const string ReadAlterations = "read:alterations";
        public const string ReadEnvironments = "read:environments";
        public const string ReadSecrets = "read:secrets";
        public const string ReadTenants = "read:tenants";
        public const string ReadWorkflowContextProviderDescriptors = "read:workflow-context-provider-descriptors";
        public const string ReadActivityExecution = "read:activity-execution";
        public const string ReadCommitStrategies = "read:commit-strategies";
        public const string ReadIncidentStrategies = "read:incident-strategies";
        public const string ReadLogPersistenceStrategies = "read:log-persistence-strategies";
        public const string ReadWorkflowActivationStrategies = "read:workflow-activation-strategies";
        public const string ReadWorkflowDefinitions = "read:workflow-definitions";
        public const string ReadWorkflowInstances = "read:workflow-instances";
        public const string ReadActivityTypeDefinitions = "read:activity-type-definitions";
        public const string ReadResilience = "read:resilience";
        public const string ReadResilienceStrategies = "read:resilience:strategies";
        public const string ReadResilienceRetries = "read:resilience:retries";
        public const string ReadActivityDescriptorsOptions = "read:activity-descriptors-options";
        public const string ReadActivityDescriptors = "read:activity-descriptors";
        public const string ReadInstalledFeatures = "read:installed-features";
        public const string ReadExpressionDescriptors = "read:expression-descriptors";
        public const string ReadStorageDrivers = "read:storage-drivers";
        public const string ReadVariableDescriptors = "read:variable-descriptors";
    }

    public static class Retract
    {
        public const string RetractWorkflowDefinitions = "retract:workflow-definitions";
    }

    public static class Run
    {
        public const string RunAlterations = "run:alterations";
    }

    public static class Secrets
    {
        public const string SecretsDelete = "secrets:delete";
        public const string SecretsWrite = "secrets:write";
        public const string SecretsRead = "secrets:read";
    }

    public static class Tasks
    {
        public const string TasksComplete = "tasks:complete";
    }

    public static class Trigger
    {
        public const string TriggerEvent = "trigger:event";
    }

    public static class Write
    {
        public const string WriteSecrets = "write:secrets";
        public const string WriteTenants = "write:tenants";
        public const string WriteWorkflowDefinitions = "write:workflow-definitions";
        public const string WriteWorkflowInstances = "write:workflow-instances";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(AbpElsaPermissions));
    }
}
