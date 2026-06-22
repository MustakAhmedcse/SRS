namespace SalesCom.Application.Commands.Approvals.UpdateApprovalFlow;

using FluentValidation;

internal sealed class UpdateApprovalFlowValidator : AbstractValidator<UpdateApprovalFlowCommand>
{
    public UpdateApprovalFlowValidator()
    {
        RuleFor(x => x.FlowName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
    }
}
