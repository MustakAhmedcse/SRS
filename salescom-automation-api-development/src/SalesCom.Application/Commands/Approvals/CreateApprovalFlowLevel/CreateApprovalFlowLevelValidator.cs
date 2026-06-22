namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevel;

using FluentValidation;

internal sealed class CreateApprovalFlowLevelValidator : AbstractValidator<CreateApprovalFlowLevelCommand>
{
    public CreateApprovalFlowLevelValidator()
    {
        RuleFor(x => x.ApprovalFlowId).GreaterThan(0);
        RuleFor(x => x.ApprovalType).IsInEnum();
        RuleFor(x => x.LevelName).NotEmpty().MaximumLength(200);
    }
}
