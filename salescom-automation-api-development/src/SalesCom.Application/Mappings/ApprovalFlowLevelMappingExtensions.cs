namespace SalesCom.Application.Mappings;

using SalesCom.Application.Responses;
using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Approvals;

/// <summary>Entity-to-response mapping for the approval-flow-level feature, kept out of the plain entities.</summary>
internal static class ApprovalFlowLevelMappingExtensions
{
    public static ApprovalFlowLevelResponse ToResponse(this ApprovalFlowLevel level) =>
        new(
            level.Id,
            level.ApprovalFlowId,
            (int)level.ApprovalType,
            level.LevelName,
            level.LevelOrder,
            level.CreatedAt,
            level.UpdatedAt,
            level.CreatedBy,
            level.UpdatedBy);

    public static ApprovalTypeResponse ToResponse(this ApprovalTypeDefinition definition) =>
        new((int)definition.Type, definition.Name, definition.SortOrder, definition.Phase.ToString());
}
