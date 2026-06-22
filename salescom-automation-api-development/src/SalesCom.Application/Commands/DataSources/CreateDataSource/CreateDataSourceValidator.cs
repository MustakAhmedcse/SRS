namespace SalesCom.Application.Commands.DataSources.CreateDataSource;

using FluentValidation;

internal sealed class CreateDataSourceValidator : AbstractValidator<CreateDataSourceCommand>
{
    public CreateDataSourceValidator()
    {
        RuleFor(x => x.SourceTableName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TableDescription).MaximumLength(1000);
    }
}
