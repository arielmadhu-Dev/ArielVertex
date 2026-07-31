using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

// Role-based appraisal forms. HR configures, per role, two independent forms:
//   Self    — the employee's reflective self-assessment (ratings + open questions)
//   Manager — the reporting manager's evaluation scorecard (rated areas, optionally weighted)
// Both stages' scores are blended into the final rating using an HR-configured split.

/// <summary>An HR-configured appraisal form for one role and one stage.</summary>
public class AppraisalFormTemplate : BaseEntity
{
    /// <summary>Role / designation this form applies to, e.g. "Software Engineer".</summary>
    public string Role { get; set; } = string.Empty;
    public AppraisalFormVariant Variant { get; set; }

    /// <summary>When true each rated area carries its own %; otherwise areas are averaged equally.</summary>
    public bool Weighted { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<AppraisalFormArea> Areas { get; set; } = new List<AppraisalFormArea>();
}

/// <summary>One row on a form — a rated evaluation area or an open question.</summary>
public class AppraisalFormArea : BaseEntity
{
    public int TemplateId { get; set; }
    public AppraisalFormTemplate? Template { get; set; }

    public string Name { get; set; } = string.Empty;
    public AppraisalAreaType Type { get; set; } = AppraisalAreaType.Rating;

    /// <summary>Percentage weight, used only when the template is Weighted and Type is Rating.</summary>
    public int Weight { get; set; }

    /// <summary>"if applicable" — the rater may mark N/A; the weight is then redistributed.</summary>
    public bool AllowNa { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A rating / answer captured against one form area for one appraisal stage.</summary>
public class AppraisalAreaScore : BaseEntity
{
    public int AppraisalId { get; set; }
    public Appraisal? Appraisal { get; set; }

    /// <summary>Which stage produced this score — Self or Manager.</summary>
    public AppraisalFormVariant Stage { get; set; }

    /// <summary>Captured by name so historical appraisals survive later template edits.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>1-5 for rated areas; null for open questions or when marked N/A.</summary>
    public int? Rating { get; set; }

    /// <summary>Free-text comment, or the answer body for an open question.</summary>
    public string? Comment { get; set; }

    public bool NotApplicable { get; set; }
}
