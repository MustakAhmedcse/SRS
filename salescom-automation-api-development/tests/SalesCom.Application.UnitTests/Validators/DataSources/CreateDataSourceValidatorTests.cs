namespace SalesCom.Application.UnitTests.Validators.DataSources;

using SalesCom.Application.Commands.DataSources.CreateDataSource;

public sealed class CreateDataSourceValidatorTests
{
    private readonly CreateDataSourceValidator _sut = new();

    private static CreateDataSourceCommand Valid() => new("ev_recharge_com", "ok", true);

    [Fact]
    public void Valid_command_passes()
    {
        _sut.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Null_description_is_allowed()
    {
        _sut.Validate(Valid() with { TableDescription = null }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_source_table_fails()
    {
        _sut.Validate(Valid() with { SourceTableName = "" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Source_table_over_200_chars_fails()
    {
        _sut.Validate(Valid() with { SourceTableName = new string('t', 201) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Description_over_1000_chars_fails()
    {
        _sut.Validate(Valid() with { TableDescription = new string('d', 1001) }).IsValid.Should().BeFalse();
    }
}
