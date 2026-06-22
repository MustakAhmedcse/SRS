namespace SalesCom.Application.Commands.Approvals.CreateApprovalFlow;

using FluentValidation;

internal sealed class CreateApprovalFlowValidator : AbstractValidator<CreateApprovalFlowCommand>
{
    public CreateApprovalFlowValidator()
    {
        RuleFor(x => x.FlowName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
    }
}
