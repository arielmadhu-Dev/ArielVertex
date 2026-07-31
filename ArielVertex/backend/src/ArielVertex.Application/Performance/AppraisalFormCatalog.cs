using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Performance;

/// <summary>
/// Built-in starting forms per role. HR can edit and save these — a saved template always wins.
/// Until a role is customised these defaults are returned so the appraisal is never blank.
/// </summary>
public static class AppraisalFormCatalog
{
    public readonly record struct Area(string Name, AppraisalAreaType Type, int Weight, bool AllowNa);

    private static Area R(string name, int weight, bool allowNa = false) => new(name, AppraisalAreaType.Rating, weight, allowNa);
    private static Area T(string name) => new(name, AppraisalAreaType.Text, 0, false);

    /// <summary>Common reflective block every self-assessment ends with.</summary>
    private static IEnumerable<Area> Reflective() => new[]
    {
        T("Key achievements this cycle"),
        T("Challenges or blockers I faced"),
        T("Goals I met / missed and why"),
        T("Skills I developed"),
        T("Areas I want to improve"),
        T("Training or support I need"),
        T("My goals for the next cycle"),
    };

    /// <summary>Manager scorecards — the areas a reporting manager rates 1-5.</summary>
    private static readonly Dictionary<string, (bool Weighted, Area[] Areas)> ManagerForms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Software Engineer"] = (false, new[]
        {
            R("Technical Knowledge", 10), R("Code Quality", 10), R("Productivity", 8), R("Timely Task Delivery", 10),
            R("Problem-Solving Skills", 8), R("Bug Resolution", 8), R("Communication", 6), R("Team Collaboration", 6),
            R("Ownership & Accountability", 10), R("Learning & Skill Development", 6), R("Documentation", 5),
            R("Client Handling & Professionalism", 5, true), R("Initiative & Continuous Improvement", 8),
        }),
        ["Project Coordinator"] = (false, new[]
        {
            R("Task & Schedule Coordination", 12), R("Documentation & Reporting", 10), R("Stakeholder Communication", 10),
            R("Meeting & Follow-up Discipline", 8), R("Resource & Timesheet Tracking", 8), R("Risk & Issue Escalation", 8),
            R("Process & Compliance Adherence", 8), R("Tracker / PMS Hygiene", 8), R("Team Collaboration", 7),
            R("Ownership & Accountability", 9), R("Learning & Skill Development", 5),
            R("Client Handling & Professionalism", 4, true), R("Initiative & Continuous Improvement", 3),
        }),
        ["Project Manager"] = (true, new[]
        {
            R("Project Planning & Estimation", 12), R("Delivery & Milestone Management", 14), R("Scope & Change Management", 9),
            R("Risk & Issue Management", 9), R("Resource Utilization & Budget", 9), R("Client & Stakeholder Management", 12),
            R("Team Leadership & Mentoring", 10), R("Quality Governance", 7), R("Communication & Reporting", 7),
            R("Conflict Resolution", 4), R("Process & Compliance Adherence", 4), R("Vendor / Cross-team Coordination", 3, true),
        }),
        ["HR Manager"] = (true, new[]
        {
            R("Recruitment & Talent Acquisition", 15), R("Onboarding & Induction", 9), R("Employee Engagement", 12),
            R("Performance Management Process", 12), R("Policy Compliance & Documentation", 10),
            R("Employee Relations & Grievance Handling", 10), R("Learning & Development Enablement", 8),
            R("HR Analytics & Reporting", 7), R("Confidentiality & Professional Ethics", 8),
            R("Leadership & Stakeholder Partnering", 6), R("Payroll & Attendance Accuracy", 3, true),
        }),
        ["Business Development Executive"] = (true, new[]
        {
            R("Lead Generation & Prospecting", 14), R("Target / Quota Achievement", 16), R("Client Relationship Management", 12),
            R("Proposal & Presentation Quality", 9), R("Negotiation & Deal Closure", 12), R("Market & Competitor Awareness", 7),
            R("CRM Hygiene & Reporting", 7), R("Follow-up Discipline", 8), R("Communication & Professionalism", 7),
            R("Cross-team Coordination (Pre-sales / Delivery)", 5), R("Learning & Skill Development", 3),
        }),
        ["QA Engineer"] = (false, new[]
        {
            R("Test Coverage & Case Design", 12), R("Defect Detection Effectiveness", 12), R("Automation Contribution", 10),
            R("Regression & Release Readiness", 10), R("Attention to Detail", 9), R("Timely Test Delivery", 9),
            R("Documentation & Reporting", 8), R("Communication", 7), R("Team Collaboration", 7),
            R("Ownership & Accountability", 9), R("Learning & Skill Development", 7),
        }),
    };

    /// <summary>Short role-specific self-ratings; the reflective block is appended to all of them.</summary>
    private static readonly Dictionary<string, string[]> SelfRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Software Engineer"] = new[] { "Technical Knowledge", "Quality of My Work", "Timely Delivery", "Collaboration & Communication", "Ownership" },
        ["Project Coordinator"] = new[] { "Coordination & Follow-up", "Documentation Accuracy", "Communication", "Ownership" },
        ["Project Manager"] = new[] { "Delivery Ownership", "Client Management", "Team Leadership", "Risk Handling" },
        ["HR Manager"] = new[] { "Recruitment Delivery", "Employee Engagement", "Compliance & Ethics", "Stakeholder Partnering" },
        ["Business Development Executive"] = new[] { "Target Achievement", "Client Relationships", "Follow-up Discipline", "Market Awareness" },
        ["QA Engineer"] = new[] { "Test Coverage", "Defect Detection", "Automation Contribution", "Ownership" },
    };

    /// <summary>Roles that ship with a built-in form.</summary>
    public static IReadOnlyList<string> Roles => ManagerForms.Keys.OrderBy(k => k).ToList();

    /// <summary>Fallback used for any role without a tailored form.</summary>
    private static readonly Area[] GenericManager =
    {
        R("Quality of Work", 20), R("Productivity", 15), R("Timely Delivery", 15), R("Communication", 10),
        R("Team Collaboration", 10), R("Ownership & Accountability", 15), R("Learning & Skill Development", 10),
        R("Initiative & Continuous Improvement", 5),
    };

    public static (bool Weighted, IReadOnlyList<Area> Areas) Default(string role, AppraisalFormVariant variant)
    {
        if (variant == AppraisalFormVariant.Manager)
            return ManagerForms.TryGetValue(role, out var m) ? (m.Weighted, m.Areas) : (false, GenericManager);

        var names = SelfRatings.TryGetValue(role, out var s)
            ? s
            : new[] { "Quality of My Work", "Timely Delivery", "Collaboration & Communication", "Ownership" };
        return (false, names.Select(n => R(n, 0)).Concat(Reflective()).ToList());
    }
}
