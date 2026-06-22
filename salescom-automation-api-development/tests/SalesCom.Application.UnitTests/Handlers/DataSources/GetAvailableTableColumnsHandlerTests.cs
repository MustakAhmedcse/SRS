namespace SalesCom.Application.UnitTests.Handlers.DataSources;

using SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;
using SalesCom.Domain.Interfaces;

public sealed class GetAvailableTableColumnsHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private GetAvailableTableColumnsHandler CreateSut() => new(_unitOfWork);

    private void ColumnsReturn(params AvailableColumnResponse[] rows) =>
        _unitOfWork.QueryAsync<AvailableColumnResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object[]>())
            .Returns(rows.ToList());

    [Fact]
    public async Task Returns_SourceTableNotFound_when_no_columns()
    {
        ColumnsReturn();

        var result = await CreateSut().HandleAsync(new GetAvailableTableColumnsQuery("ghost_com"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DataSource.SourceTableNotFound");
    }

    [Fact]
    public async Task Returns_columns()
    {
        ColumnsReturn(
            new AvailableColumnResponse("msisdn", "character varying"),
            new AvailableColumnResponse("amount", "numeric"));

        var result = await CreateSut().HandleAsync(new GetAvailableTableColumnsQuery("ev_recharge_com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].ColumnName.Should().Be("msisdn");
        result.Value[0].DataType.Should().Be("character varying");
        result.Value[1].ColumnName.Should().Be("amount");
        result.Value[1].DataType.Should().Be("numeric");
    }
}
