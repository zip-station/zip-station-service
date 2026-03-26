namespace ZipStation.Models.Constants;

public static class Permissions
{
    // Tickets
    public const string TicketsView = "Tickets.View";
    public const string TicketsCreate = "Tickets.Create";
    public const string TicketsEdit = "Tickets.Edit";
    public const string TicketsDelete = "Tickets.Delete";
    public const string TicketsAssign = "Tickets.Assign";
    public const string TicketsMerge = "Tickets.Merge";
    public const string TicketsLink = "Tickets.Link";

    // Intake
    public const string IntakeView = "Intake.View";
    public const string IntakeApprove = "Intake.Approve";
    public const string IntakeDeny = "Intake.Deny";
    public const string IntakeImportHistory = "Intake.ImportHistory";
    public const string IntakeCheckNow = "Intake.CheckNow";

    // Intake Rules
    public const string IntakeRulesView = "IntakeRules.View";
    public const string IntakeRulesCreate = "IntakeRules.Create";
    public const string IntakeRulesEdit = "IntakeRules.Edit";
    public const string IntakeRulesDelete = "IntakeRules.Delete";

    // Customers
    public const string CustomersView = "Customers.View";
    public const string CustomersEdit = "Customers.Edit";

    // Canned Responses
    public const string CannedResponsesView = "CannedResponses.View";
    public const string CannedResponsesCreate = "CannedResponses.Create";
    public const string CannedResponsesEdit = "CannedResponses.Edit";
    public const string CannedResponsesDelete = "CannedResponses.Delete";

    // Projects
    public const string ProjectsView = "Projects.View";
    public const string ProjectsCreate = "Projects.Create";
    public const string ProjectsSettings = "Projects.Settings";
    public const string ProjectsDelete = "Projects.Delete";

    // Members
    public const string MembersView = "Members.View";
    public const string MembersInvite = "Members.Invite";
    public const string MembersRemove = "Members.Remove";
    public const string MembersEdit = "Members.Edit";

    // Roles
    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesEdit = "Roles.Edit";
    public const string RolesDelete = "Roles.Delete";

    // Alerts
    public const string AlertsView = "Alerts.View";
    public const string AlertsCreate = "Alerts.Create";
    public const string AlertsEdit = "Alerts.Edit";
    public const string AlertsDelete = "Alerts.Delete";

    // Reports
    public const string ReportsView = "Reports.View";
    public const string ReportsCreate = "Reports.Create";
    public const string ReportsEdit = "Reports.Edit";
    public const string ReportsDelete = "Reports.Delete";

    // Audit Log
    public const string AuditLogView = "AuditLog.View";

    // Dashboard
    public const string DashboardView = "Dashboard.View";

    /// <summary>
    /// All permissions grouped by category for the role management UI.
    /// </summary>
    public static readonly Dictionary<string, string[]> Groups = new()
    {
        ["Dashboard"] = [DashboardView],
        ["Tickets"] = [TicketsView, TicketsCreate, TicketsEdit, TicketsDelete, TicketsAssign, TicketsMerge, TicketsLink],
        ["Intake"] = [IntakeView, IntakeApprove, IntakeDeny, IntakeImportHistory, IntakeCheckNow],
        ["Intake Rules"] = [IntakeRulesView, IntakeRulesCreate, IntakeRulesEdit, IntakeRulesDelete],
        ["Customers"] = [CustomersView, CustomersEdit],
        ["Canned Responses"] = [CannedResponsesView, CannedResponsesCreate, CannedResponsesEdit, CannedResponsesDelete],
        ["Projects"] = [ProjectsView, ProjectsCreate, ProjectsSettings, ProjectsDelete],
        ["Members"] = [MembersView, MembersInvite, MembersRemove, MembersEdit],
        ["Roles"] = [RolesView, RolesCreate, RolesEdit, RolesDelete],
        ["Alerts"] = [AlertsView, AlertsCreate, AlertsEdit, AlertsDelete],
        ["Reports"] = [ReportsView, ReportsCreate, ReportsEdit, ReportsDelete],
        ["Audit Log"] = [AuditLogView],
    };

    /// <summary>
    /// Flat list of all permissions.
    /// </summary>
    public static readonly string[] All = Groups.Values.SelectMany(p => p).ToArray();
}
