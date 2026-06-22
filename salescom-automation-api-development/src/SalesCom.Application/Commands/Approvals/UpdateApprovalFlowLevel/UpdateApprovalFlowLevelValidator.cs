namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevel;

using FluentValidation;

internal sealed class UpdateApprovalFlowLevelValidator : AbstractValidator<UpdateApprovalFlowLevelCommand>
{
    public UpdateApprovalFlowLevelValidator()
    {
        RuleFor(x => x.LevelName).NotEmpty().MaximumLength(200);
    }
}
