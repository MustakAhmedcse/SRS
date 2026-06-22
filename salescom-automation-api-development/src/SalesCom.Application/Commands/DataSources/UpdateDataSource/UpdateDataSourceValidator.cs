namespace SalesCom.Application.Commands.DataSources.UpdateDataSource;

using FluentValidation;

internal sealed class UpdateDataSourceValidator : AbstractValidator<UpdateDataSourceCommand>
{
    public UpdateDataSourceValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.TableDescription).MaximumLength(1000);
    }
}
