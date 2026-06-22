namespace SalesCom.Application.Mappings;

using SalesCom.Application.Responses;
using SalesCom.Domain.Entities.Approvals;

/// <summary>Entity-to-response mapping for the approval-flow feature, kept out of the plain entities.</summary>
internal static class ApprovalFlowMappingExtensions
{
    public static ApprovalFlowResponse ToResponse(this ApprovalFlow flow) =>
        new(
            flow.Id,
            flow.FlowName,
            flow.Description,
            flow.CreatedAt,
            flow.UpdatedAt,
            flow.CreatedBy,
            flow.UpdatedBy);
}
