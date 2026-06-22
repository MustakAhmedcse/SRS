namespace SalesCom.Application.UnitTests.Validators.DataSources;

using SalesCom.Application.Commands.DataSources.UpdateDataSource;

public sealed class UpdateDataSourceValidatorTests
{
    private readonly UpdateDataSourceValidator _sut = new();

    private static UpdateDataSourceCommand Valid() => new(7, "ok", true);

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
    public void Non_positive_id_fails()
    {
        _sut.Validate(Valid() with { Id = 0 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Description_over_1000_chars_fails()
    {
        _sut.Validate(Valid() with { TableDescription = new string('d', 1001) }).IsValid.Should().BeFalse();
    }
}
