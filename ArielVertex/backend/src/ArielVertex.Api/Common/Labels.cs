using System.Text.RegularExpressions;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Api.Common;

/// <summary>Human-friendly labels for enums so the UI shows organizational language, not tokens.</summary>
public static class Labels
{
    public static string Role(PortalRole r) => r switch
    {
        PortalRole.SuperAdmin => "Super Admin",
        PortalRole.CeoAdmin => "CEO / Admin",
        PortalRole.HrManager => "HR Manager",
        PortalRole.HrDirector => "HR Director",
        PortalRole.HrRecruiter => "HR Recruiter",
        PortalRole.ProjectManager => "Project Manager",
        PortalRole.ProjectCoordinator => "Project Coordinator",
        PortalRole.BusinessDirector => "Business Director",
        PortalRole.BusinessManager => "Business Manager",
        PortalRole.BusinessPerson => "Business Person",
        PortalRole.TechnicalLead => "Technical Lead",
        PortalRole.SystemAdmin => "System Admin",
        PortalRole.Frontdesk => "Front Desk",
        PortalRole.Accountant => "Accountant",
        _ => "Employee"
    };

    public static string FeedbackStatus(FeedbackStatus s) => s switch
    {
        Domain.Enums.FeedbackStatus.Draft => "Draft",
        Domain.Enums.FeedbackStatus.Submitted => "Awaiting HR Approval",
        Domain.Enums.FeedbackStatus.RevisionRequested => "Revision Requested",
        Domain.Enums.FeedbackStatus.Approved => "Approved",
        _ => "Published"
    };

    public static string ResourceStatus(ResourceRequestStatus s) => Spaced(s.ToString());

    public static string Audit(AuditAction a) => Spaced(a.ToString());

    public static string ExpenseStatus(ExpenseStatus s) => Spaced(s.ToString());

    public static string ExpenseCategory(ExpenseCategory c) => Spaced(c.ToString());

    public static string BillStatus(BillStatus s) => Spaced(s.ToString());

    public static string PettyCashEntryStatus(PettyCashEntryStatus s) => Spaced(s.ToString());

    public static string PipStatus(PipStatus s) => Spaced(s.ToString());

    public static string PipOutcome(PipOutcome o) => Spaced(o.ToString());

    public static string MinutesStatus(MeetingMinuteStatus s) => s switch
    {
        MeetingMinuteStatus.Draft => "Draft notes",
        MeetingMinuteStatus.Preview => "Preview — awaiting approval",
        MeetingMinuteStatus.Approved => "Approved — ready to send",
        _ => "Sent to attendees"
    };

    /// <summary>"NeedMoreInformation" -> "Need More Information".</summary>
    private static string Spaced(string pascal) =>
        Regex.Replace(pascal, "(?<=[a-z])(?=[A-Z])", " ");
}
