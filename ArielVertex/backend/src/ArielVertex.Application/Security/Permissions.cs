using System.Linq;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Security;

/// <summary>
/// Capability catalogue. Authorization is capability-based (not role-string based) so the
/// UI and API both reason about what a user can *do*, and roles simply grant capabilities.
/// Data-level scoping (which project) is enforced separately by the project-scoped handler.
/// </summary>
public static class Permissions
{
    public const string ProjectsViewAll     = "projects.view.all";
    public const string ProjectsView        = "projects.view";
    public const string ProjectsCreate      = "projects.create";
    public const string ProjectsManage      = "projects.manage";
    public const string MembersManage       = "members.manage";
    public const string DocumentsUpload     = "documents.upload";
    public const string DocumentsDelete     = "documents.delete";
    public const string CallsManage         = "calls.manage";
    public const string StatusSubmit        = "status.submit";
    public const string StatusViewAll       = "status.view.all";
    public const string ReviewsRequest      = "reviews.request";
    public const string ReviewsSchedule     = "reviews.schedule";
    public const string ReviewsSubmit       = "reviews.submit";
    public const string FeedbackSubmit      = "feedback.submit";
    public const string FeedbackApprove     = "feedback.approve";
    public const string FeedbackViewAll     = "feedback.view.all";
    public const string PerformanceViewOwn  = "performance.view.own";
    public const string PerformanceViewAll  = "performance.view.all";
    public const string PerformancePublish  = "performance.publish";
    public const string ResourcesRequest    = "resources.request";
    public const string ResourcesManage     = "resources.manage";
    public const string ResourcesViewAll    = "resources.view.all";
    public const string ReportsView         = "reports.view";
    public const string EmployeesManage     = "employees.manage";   // view / edit the employee directory
    public const string RolesManage         = "roles.manage";       // change another user's portal role (Super Admin only)
    public const string SyncRun             = "sync.run";
    public const string AuditView           = "audit.view";
    public const string AdminSettings       = "admin.settings";
    public const string ExpensesManage      = "expenses.manage";   // Front desk: create / request / pay
    public const string ExpensesApprove     = "expenses.approve";  // HR Director / Accountant
    public const string ExpensesViewAll     = "expenses.view.all";
    public const string ExpensesConfigure   = "expenses.configure";
    public const string PipView             = "pip.view";
    public const string PipManage           = "pip.manage";
    public const string ConfigManage        = "config.manage";     // admin portal configuration
    public const string MinutesManage       = "minutes.manage";    // capture notes → minutes → send (PC + HR)

    /// <summary>Every capability — the full catalogue used to register one policy per capability.</summary>
    public static readonly string[] All =
    {
        ProjectsViewAll, ProjectsView, ProjectsCreate, ProjectsManage, MembersManage,
        DocumentsUpload, DocumentsDelete, CallsManage, StatusSubmit, StatusViewAll,
        ReviewsRequest, ReviewsSchedule, ReviewsSubmit, FeedbackSubmit, FeedbackApprove,
        FeedbackViewAll, PerformanceViewOwn, PerformanceViewAll, PerformancePublish,
        ResourcesRequest, ResourcesManage, ResourcesViewAll, ReportsView, EmployeesManage,
        RolesManage, SyncRun, AuditView, AdminSettings,
        ExpensesManage, ExpensesApprove, ExpensesViewAll, ExpensesConfigure,
        PipView, PipManage, ConfigManage, MinutesManage
    };

    // Capabilities that only make sense for an individual contributor, never granted to the
    // all-powerful admin roles (matrix: Submit Status Update / View Own Performance are not admin actions).
    private static readonly HashSet<string> ContributorOnly = new() { StatusSubmit, PerformanceViewOwn };

    /// <summary>Everything except contributor-only capabilities — granted to Super Admin (and HR Director).</summary>
    public static readonly string[] SuperAdminGrant = All.Where(p => !ContributorOnly.Contains(p)).ToArray();

    /// <summary>Capabilities granted to each portal role (aligned to the Permission Matrix).</summary>
    public static IReadOnlyCollection<string> For(PortalRole role) => role switch
    {
        PortalRole.SuperAdmin => SuperAdminGrant,

        // HR Director is a Super Admin equivalent (per business decision) — full access incl. user/role management.
        PortalRole.HrDirector => SuperAdminGrant,

        // CEO: view-oriented, plus Request Review and Add Feedback (matrix section 5).
        PortalRole.CeoAdmin => new[]
        {
            ProjectsViewAll, ProjectsView, PerformanceViewAll, FeedbackViewAll,
            ResourcesViewAll, ReportsView, AuditView, ExpensesViewAll, PipView,
            ReviewsRequest, FeedbackSubmit
        },

        PortalRole.SystemAdmin => new[]
        {
            SyncRun, AdminSettings, AuditView, EmployeesManage, ProjectsViewAll, ProjectsView,
            ExpensesConfigure, ExpensesViewAll, ConfigManage
        },

        // HR Manager: HR duties + Request Review (request-only) and Add Feedback. No RolesManage.
        PortalRole.HrManager => new[]
        {
            ProjectsViewAll, ProjectsView, EmployeesManage, ReviewsRequest,
            FeedbackSubmit, FeedbackApprove, FeedbackViewAll, PerformanceViewAll, PerformancePublish,
            ResourcesManage, ResourcesViewAll, ReportsView, ExpensesViewAll, PipView, PipManage,
            MinutesManage
        },

        // Accountant: approves & sees all expenses (finance owner). No own-performance dashboard.
        PortalRole.Accountant => new[]
        {
            ProjectsView, ExpensesApprove, ExpensesViewAll
        },

        // Front desk: raises and manages internal expenses / payment requests. No own-performance dashboard.
        PortalRole.Frontdesk => new[]
        {
            ProjectsView, ExpensesManage
        },

        // Project Manager: assigned project management + Schedule/Request Review, Add Feedback, Submit Status.
        PortalRole.ProjectManager => new[]
        {
            ProjectsView, ProjectsCreate, ProjectsManage, MembersManage, DocumentsUpload,
            DocumentsDelete, CallsManage, StatusSubmit, StatusViewAll,
            ReviewsRequest, ReviewsSchedule, ReviewsSubmit,
            FeedbackSubmit, ResourcesRequest, ReportsView, MinutesManage
        },

        // Project Coordinator: same delivery capabilities as PM (may create projects, per business decision).
        PortalRole.ProjectCoordinator => new[]
        {
            ProjectsView, ProjectsCreate, ProjectsManage, MembersManage, DocumentsUpload,
            DocumentsDelete, CallsManage, StatusSubmit, StatusViewAll,
            ReviewsRequest, ReviewsSchedule, ReviewsSubmit,
            FeedbackSubmit, ResourcesRequest, ReportsView, MinutesManage
        },

        PortalRole.TechnicalLead => new[]
        {
            ProjectsView, DocumentsUpload, StatusViewAll, ReviewsSubmit
        },

        PortalRole.BusinessDirector or PortalRole.BusinessManager or PortalRole.BusinessPerson => new[]
        {
            ProjectsView
        },

        // Employee / QA / Developer
        _ => new[] { ProjectsView, StatusSubmit, PerformanceViewOwn }
    };
}
