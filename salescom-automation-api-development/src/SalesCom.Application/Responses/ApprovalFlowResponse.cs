namespace SalesCom.Application.Responses;

using System.Text.Json.Serialization;

/// <summary>One approval flow row for the list endpoint — scalar fields only, no level payload.</summary>
public sealed record ApprovalFlowResponse(
    long Id,
    string FlowName,
    string? Description,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn,
    string CreatedBy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? UpdatedBy);
