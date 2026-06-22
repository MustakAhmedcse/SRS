namespace SalesCom.Application.Mappings;

using SalesCom.Application.Responses;
using SalesCom.Domain.Entities.Approvals;

/// <summary>Entity-to-response mapping for the approval-flow-level-user feature, kept out of the plain entities.</summary>
internal static class ApprovalFlowLevelUserMappingExtensions
{
    public static ApprovalFlowLevelUserResponse ToResponse(this ApprovalFlowLevelUser levelUser) =>
        new(levelUser.Id, levelUser.ApprovalFlowLevelId, levelUser.UserId);
}
