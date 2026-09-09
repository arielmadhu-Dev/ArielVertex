using ArielVertex.Api.Common;
using ArielVertex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/meta")]
public class MetaController : ApiControllerBase
{
    private static object[] Opts<TEnum>(Func<TEnum, string> label) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => (object)new { value = v.ToString(), label = label(v) }).ToArray();

    /// <summary>Enum option lists so the SPA never hard-codes server enums.</summary>
    [HttpGet("enums")]
    public IActionResult Enums() => Ok(new
    {
        roles = Opts<PortalRole>(Labels.Role),
        employeeStatuses = Opts<EmployeeStatus>(s => s.ToString()),
        projectRoles = Opts<ProjectRole>(r => System.Text.RegularExpressions.Regex.Replace(r.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        projectStatus = Opts<ProjectStatus>(s => s.ToString()),
        projectHealth = Opts<ProjectHealth>(h => h.ToString()),
        priorities = Opts<Priority>(p => p.ToString()),
        reviewTypes = Opts<ReviewType>(t => System.Text.RegularExpressions.Regex.Replace(t.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        resourceStatuses = Opts<ResourceRequestStatus>(Labels.ResourceStatus),
        documentCategories = Opts<DocumentCategory>(c => System.Text.RegularExpressions.Regex.Replace(c.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        visibilities = Opts<Visibility>(v => System.Text.RegularExpressions.Regex.Replace(v.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        updateStatuses = Opts<UpdateStatus>(s => System.Text.RegularExpressions.Regex.Replace(s.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        callTypes = Opts<CallType>(t => t.ToString()),
        expenseCategories = Opts<ExpenseCategory>(Labels.ExpenseCategory),
        expenseStatuses = Opts<ExpenseStatus>(Labels.ExpenseStatus),
        billStatuses = Opts<BillStatus>(Labels.BillStatus),
        pettyCashStatuses = Opts<PettyCashEntryStatus>(Labels.PettyCashEntryStatus),
        helpdeskTicketStatuses = Opts<HelpdeskTicketStatus>(Labels.HelpdeskTicketStatusLabel),
        helpdeskTicketPriorities = Opts<HelpdeskTicketPriority>(Labels.HelpdeskTicketPriorityLabel),
        helpdeskTicketCategories = Opts<HelpdeskTicketCategory>(Labels.HelpdeskTicketCategoryLabel),
        assetCategories = Opts<AssetCategory>(Labels.AssetCategory),
        assetConditions = Opts<AssetCondition>(Labels.AssetConditionLabel),
        assetStatuses = Opts<AssetStatus>(Labels.AssetStatusLabel),
        assetRequestTypes = Opts<AssetRequestType>(Labels.AssetRequestTypeLabel),
        assetRequestStatuses = Opts<AssetRequestStatus>(Labels.AssetRequestStatusLabel),
        // Performance-management enums (ported modules)
        cycleStatuses = Opts<CycleStatus>(s => s.ToString()),
        appraisalStages = Opts<AppraisalStage>(s => System.Text.RegularExpressions.Regex.Replace(s.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        goalStatuses = Opts<GoalStatus>(s => System.Text.RegularExpressions.Regex.Replace(s.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        promotionStages = Opts<PromotionStage>(s => System.Text.RegularExpressions.Regex.Replace(s.ToString(), "(?<=[a-z])(?=[A-Z])", " ")),
        trainingStatuses = Opts<TrainingStatus>(s => s.ToString()),
        goalCategories = new object[]
        {
            new { value = "Project Delivery", label = "Project Delivery" },
            new { value = "Technical Growth", label = "Technical Growth" },
            new { value = "Quality & Ownership", label = "Quality & Ownership" },
            new { value = "Collaboration", label = "Collaboration" },
            new { value = "Leadership", label = "Leadership" },
            new { value = "Process & Compliance", label = "Process & Compliance" }
        }
    });
}
