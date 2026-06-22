namespace SalesCom.Application.Commands.Account.VerifyAuthToken;

using FluentValidation;

internal sealed class VerifyAuthTokenValidator : AbstractValidator<VerifyAuthTokenCommand>
{
    public VerifyAuthTokenValidator()
    {
        RuleFor(c => c.AuthToken).NotEmpty().MaximumLength(2048);
    }
}
