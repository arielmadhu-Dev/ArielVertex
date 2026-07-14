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
    public const string EmployeesManage     = "employees.manage";
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

    /// <summary>Every capability — granted wholesale to Super Admin.</summary>
    public static readonly string[] All =
    {
        ProjectsViewAll, ProjectsView, ProjectsCreate, ProjectsManage, MembersManage,
        DocumentsUpload, DocumentsDelete, CallsManage, StatusSubmit, StatusViewAll,
        ReviewsRequest, ReviewsSchedule, ReviewsSubmit, FeedbackSubmit, FeedbackApprove,
        FeedbackViewAll, PerformanceViewOwn, PerformanceViewAll, PerformancePublish,
        ResourcesRequest, ResourcesManage, ResourcesViewAll, ReportsView, EmployeesManage,
        SyncRun, AuditView, AdminSettings,
        ExpensesManage, ExpensesApprove, ExpensesViewAll, ExpensesConfigure,
        PipView, PipManage, ConfigManage, MinutesManage
    };

    /// <summary>Capabilities granted to each portal role (spec section 4 access model).</summary>
    public static IReadOnlyCollection<string> For(PortalRole role) => role switch
    {
        PortalRole.SuperAdmin => All,

        PortalRole.CeoAdmin => new[]
        {
            ProjectsViewAll, ProjectsView, PerformanceViewAll, FeedbackViewAll,
            ResourcesViewAll, ReportsView, AuditView, ExpensesViewAll, PipView
        },

        PortalRole.SystemAdmin => new[]
        {
            SyncRun, AdminSettings, AuditView, EmployeesManage, ProjectsViewAll, ProjectsView,
            ExpensesConfigure, ExpensesViewAll, ConfigManage
        },

        PortalRole.HrManager => new[]
        {
            ProjectsViewAll, ProjectsView, EmployeesManage, ReviewsRequest,
            FeedbackApprove, FeedbackViewAll, PerformanceViewAll, PerformancePublish,
            ResourcesManage, ResourcesViewAll, ReportsView, ExpensesViewAll, PipView, PipManage,
            MinutesManage
        },

        // HR Director additionally approves expenses, manages PIPs, and configures the portal.
        PortalRole.HrDirector => new[]
        {
            ProjectsViewAll, ProjectsView, EmployeesManage, ReviewsRequest,
            FeedbackApprove, FeedbackViewAll, PerformanceViewAll, PerformancePublish,
            ResourcesManage, ResourcesViewAll, ReportsView,
            ExpensesViewAll, ExpensesApprove, ExpensesConfigure, PipView, PipManage, ConfigManage,
            MinutesManage
        },

        // Accountant: approves & sees all expenses (finance owner).
        PortalRole.Accountant => new[]
        {
            ProjectsView, PerformanceViewOwn, ExpensesApprove, ExpensesViewAll
        },

        // Front desk: raises and manages internal expenses / payment requests.
        PortalRole.Frontdesk => new[]
        {
            ProjectsView, PerformanceViewOwn, ExpensesManage
        },

        PortalRole.ProjectManager => new[]
        {
            ProjectsView, ProjectsCreate, ProjectsManage, MembersManage, DocumentsUpload,
            DocumentsDelete, CallsManage, StatusViewAll, ReviewsSchedule, ReviewsSubmit,
            FeedbackSubmit, ResourcesRequest, ReportsView, MinutesManage
        },

        PortalRole.ProjectCoordinator => new[]
        {
            ProjectsView, ProjectsManage, MembersManage, DocumentsUpload, DocumentsDelete,
            CallsManage, StatusViewAll, ReviewsSchedule, ReviewsSubmit, FeedbackSubmit,
            ResourcesRequest, ReportsView, MinutesManage
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
