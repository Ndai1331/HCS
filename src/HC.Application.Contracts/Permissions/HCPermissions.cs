namespace HC.Permissions;

public static class HCPermissions
{
    public const string GroupName = "HC";

    public static class Dashboard
    {
        public const string DashboardGroup = GroupName + ".Dashboard";
        public const string Host = DashboardGroup + ".Host";
        public const string Tenant = DashboardGroup + ".Tenant";
    }

    public static class Workspace
    {
        public const string Default = GroupName + ".Workspace";
    }

    public static class Books
    {
        public const string Default = GroupName + ".Books";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";
    // public static class Positions
    // {
    //     public const string Default = GroupName + ".Positions";
    //     public const string Edit = Default + ".Edit";
    //     public const string Create = Default + ".Create";
    //     public const string Delete = Default + ".Delete";
    // }

    public static class MasterDatas
    {
        public const string Default = GroupName + ".MasterDatas";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
        public const string DocumentTypeDefault = Default + ".Documents";
        public const string DocumentTypeEdit = DocumentTypeDefault + ".Edit";
        public const string DocumentTypeCreate = DocumentTypeDefault + ".Create";
        public const string DocumentTypeDelete = DocumentTypeDefault + ".Delete";
        public const string SectorDefault = Default + ".Sectors";
        public const string SectorEdit = SectorDefault + ".Edit";
        public const string SectorCreate = SectorDefault + ".Create";
        public const string SectorDelete = SectorDefault + ".Delete";
        public const string StatusDefault = Default + ".Status";
        public const string StatusEdit = StatusDefault + ".Edit";
        public const string StatusCreate = StatusDefault + ".Create";
        public const string StatusDelete = StatusDefault + ".Delete";
        public const string UrgencyLevelDefault = Default + ".UrgencyLevels";
        public const string UrgencyLevelEdit = UrgencyLevelDefault + ".Edit";
        public const string UrgencyLevelCreate = UrgencyLevelDefault + ".Create";
        public const string UrgencyLevelDelete = UrgencyLevelDefault + ".Delete";
        public const string ConfidentialityLevelDefault = Default + ".ConfidentialityLevels";
        public const string ConfidentialityLevelEdit = ConfidentialityLevelDefault + ".Edit";
        public const string ConfidentialityLevelCreate = ConfidentialityLevelDefault + ".Create";
        public const string ConfidentialityLevelCreateDelete = ConfidentialityLevelDefault + ".Delete";
        public const string ProcessingMethodDefault = Default + ".ProcessingMethods";
        public const string ProcessingMethodEdit = ProcessingMethodDefault + ".Edit";
        public const string ProcessingMethodCreate = ProcessingMethodDefault + ".Create";
        public const string ProcessingMethodDelete = ProcessingMethodDefault + ".Delete";
        public const string DocumentStatusDefault = Default + ".DocumentStatus";
        public const string DocumentStatusCreate = DocumentStatusDefault + ".Create";
        public const string DocumentStatusDelete = DocumentStatusDefault + ".Delete";
        public const string SigningMethodDefault = Default + ".SigningMethods";
        public const string SigningMethodEdit = SigningMethodDefault + ".Edit";
        public const string SigningMethodCreate = SigningMethodDefault + ".Create";
        public const string SigningMethodDelete = SigningMethodDefault + ".Delete";
        public const string EventTypeDefault = Default + ".EventTypes";
        public const string EventTypeEdit = EventTypeDefault + ".Edit";
        public const string EventTypeCreate = EventTypeDefault + ".Create";
        public const string EventTypeDelete = EventTypeDefault + ".Delete";
        public const string IssuingAuthorityDefault = Default + ".IssuingAuthorities";
        public const string IssuingAuthorityEdit = IssuingAuthorityDefault + ".Edit";
        public const string IssuingAuthorityCreate = IssuingAuthorityDefault + ".Create";
        public const string IssuingAuthorityDelete = IssuingAuthorityDefault + ".Delete";
        public const string UnitDefault = Default + ".Units";
        public const string UnitEdit = UnitDefault + ".Edit";
        public const string UnitCreate = UnitDefault + ".Create";
        public const string UnitDelete = UnitDefault + ".Delete";
        public const string DepartmentDefault = Default + ".Departments";
        public const string DepartmentEdit = DepartmentDefault + ".Edit";
        public const string DepartmentCreate = DepartmentDefault + ".Create";
        public const string DepartmentDelete = DepartmentDefault + ".Delete";
        public const string PositionDefault = Default + ".Positions";
        public const string PositionEdit = PositionDefault + ".Edit";
        public const string PositionCreate = PositionDefault + ".Create";
        public const string PositionDelete = PositionDefault + ".Delete";
        public const string SurveyLocationDefault = GroupName + ".SurveyLocations";
        public const string SurveyLocationEdit = SurveyLocationDefault + ".Edit";
        public const string SurveyLocationCreate = SurveyLocationDefault + ".Create";
        public const string SurveyLocationDelete = SurveyLocationDefault + ".Delete";
        public const string SurveyCriteriaDefault = GroupName + ".SurveyCriterias";
        public const string SurveyCriteriaEdit = SurveyCriteriaDefault + ".Edit";
        public const string SurveyCriteriaCreate = SurveyCriteriaDefault + ".Create";
        public const string SurveyCriteriaDelete = SurveyCriteriaDefault + ".Delete";
        public const string SignatureSettingsDefault = GroupName + ".SignatureSettings";
        public const string SignatureSettingsEdit = SignatureSettingsDefault + ".Edit";
        public const string SignatureSettingsCreate = SignatureSettingsDefault + ".Create";
        public const string SignatureSettingsDelete = SignatureSettingsDefault + ".Delete";

        public const string ReportsDefault = Default + ".Reports";
        public const string ReportsEdit = ReportsDefault + ".Edit";
        public const string ReportsCreate = ReportsDefault + ".Create";
        public const string ReportsDelete = ReportsDefault + ".Delete";
    }

    public static class Hrs
    {
        public const string Default = GroupName + ".Hrs";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Reports
    {
        public const string Default = GroupName + ".Reports";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
        
        public const string DocumentDefault = Default + ".Documents";
        public const string DocumentEdit = DocumentDefault + ".Edit";
        public const string DocumentCreate = DocumentDefault + ".Create";
        public const string DocumentDelete = DocumentDefault + ".Delete";
        public const string ProjectDefault = Default + ".Projects";
        public const string ProjectEdit = ProjectDefault + ".Edit";
        public const string ProjectCreate = ProjectDefault + ".Create";
        public const string ProjectDelete = ProjectDefault + ".Delete";
        public const string ProjectTaskDefault = Default + ".ProjectTasks";
        public const string ProjectTaskEdit = ProjectTaskDefault + ".Edit";
        public const string ProjectTaskCreate = ProjectTaskDefault + ".Create";
        public const string ProjectTaskDelete = ProjectTaskDefault + ".Delete";
        public const string WorkflowDefault = Default + ".Workflows";
        public const string WorkflowEdit = WorkflowDefault + ".Edit";
        public const string WorkflowCreate = WorkflowDefault + ".Create";
        public const string WorkflowDelete = WorkflowDefault + ".Delete";
    }

    public static class WorkflowDefinitions
    {
        public const string Default = GroupName + ".WorkflowDefinitions";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Workflows
    {
        public const string Default = GroupName + ".Workflows";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class WorkflowTemplates
    {
        public const string Default = GroupName + ".WorkflowTemplates";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class WorkflowStepTemplates
    {
        public const string Default = GroupName + ".WorkflowStepTemplates";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    // public static class Departments
    // {
    //     public const string Default = GroupName + ".Departments";
    //     public const string Edit = Default + ".Edit";
    //     public const string Create = Default + ".Create";
    //     public const string Delete = Default + ".Delete";
    // }

    // public static class Units
    // {
    //     public const string Default = GroupName + ".Units";
    //     public const string Edit = Default + ".Edit";
    //     public const string Create = Default + ".Create";
    //     public const string Delete = Default + ".Delete";
    // }

    public static class WorkflowStepAssignments
    {
        public const string Default = GroupName + ".WorkflowStepAssignments";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Documents
    {
        public const string Default = GroupName + ".Documents";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
        public const string SubmitForSigning = Default + ".SubmitForSigning";
        public const string Send = Default + ".Send";
        public const string SubmitForApproval = Default + ".SubmitForApproval";
        public const string RejectApproval = Default + ".RejectApproval";
        public const string ApproveWithNote = Default + ".ApproveWithNote";
        public const string MyDocuments = Default + ".MyDocuments";
    }

    public static class DocumentFiles
    {
        public const string Default = GroupName + ".DocumentFiles";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DocumentWorkflowInstances
    {
        public const string Default = GroupName + ".DocumentWorkflowInstances";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DocumentAssignments
    {
        public const string Default = GroupName + ".DocumentAssignments";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DocumentHistories
    {
        public const string Default = GroupName + ".DocumentHistories";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Projects
    {
        public const string Default = GroupName + ".Projects";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class ProjectMembers
    {
        public const string Default = GroupName + ".ProjectMembers";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Tasks
    {
        public const string Default = GroupName + ".Tasks";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class ProjectTasks
    {
        public const string Default = GroupName + ".ProjectTasks";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class ProjectTaskAssignments
    {
        public const string Default = GroupName + ".ProjectTaskAssignments";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class ProjectTaskDocuments
    {
        public const string Default = GroupName + ".ProjectTaskDocuments";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class Notifications
    {
        public const string Default = GroupName + ".Notifications";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class NotificationReceivers
    {
        public const string Default = GroupName + ".NotificationReceivers";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class UserSignatures
    {
        public const string Default = GroupName + ".UserSignatures";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class CalendarEvents
    {
        public const string Default = GroupName + ".CalendarEvents";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class CalendarEventParticipants
    {
        public const string Default = GroupName + ".CalendarEventParticipants";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class SurveySessions
    {
        public const string Default = GroupName + ".SurveySessions";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class SurveyFiles
    {
        public const string Default = GroupName + ".SurveyFiles";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class SurveyResults
    {
        public const string Default = GroupName + ".SurveyResults";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class UserDepartments
    {
        public const string Default = GroupName + ".UserDepartments";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DocumentWorkflowInstanceFiles
    {
        public const string Default = GroupName + ".DocumentWorkflowInstanceFiles";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }

    public static class DocumentWorkflowInstanceLogss
    {
        public const string Default = GroupName + ".DocumentWorkflowInstanceLogss";
        public const string Edit = Default + ".Edit";
        public const string Create = Default + ".Create";
        public const string Delete = Default + ".Delete";
    }
}