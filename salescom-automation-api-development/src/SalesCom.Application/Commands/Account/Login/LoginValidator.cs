namespace SalesCom.Application.Commands.Account.Login;

using FluentValidation;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Username).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(256);
    }
}
