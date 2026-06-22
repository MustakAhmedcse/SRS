namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevelUser;

using FluentValidation;

internal sealed class UpdateApprovalFlowLevelUserValidator : AbstractValidator<UpdateApprovalFlowLevelUserCommand>
{
    public UpdateApprovalFlowLevelUserValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("UserId must be provided.");
    }
}
