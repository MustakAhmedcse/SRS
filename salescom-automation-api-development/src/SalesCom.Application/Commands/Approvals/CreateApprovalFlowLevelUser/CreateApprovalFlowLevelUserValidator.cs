namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevelUser;

using FluentValidation;

internal sealed class CreateApprovalFlowLevelUserValidator : AbstractValidator<CreateApprovalFlowLevelUserCommand>
{
    public CreateApprovalFlowLevelUserValidator()
    {
        RuleFor(x => x.ApprovalFlowLevelId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("UserId must be provided.");
    }
}
