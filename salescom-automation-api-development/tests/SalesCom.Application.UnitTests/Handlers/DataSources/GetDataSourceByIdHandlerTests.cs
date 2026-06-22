namespace SalesCom.Application.UnitTests.Handlers.DataSources;

using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

public sealed class GetDataSourceByIdHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<DataSource> _sources = Substitute.For<IGenericRepository<DataSource>>();

    public GetDataSourceByIdHandlerTests()
    {
        _unitOfWork.Repository<DataSource>().Returns(_sources);
    }

    private GetDataSourceByIdHandler CreateSut() => new(_unitOfWork);

    [Fact]
    public async Task Returns_NotFound_when_data_source_is_missing()
    {
        _sources.GetByIdAsync(99L, Arg.Any<CancellationToken>()).Returns((DataSource?)null);

        var result = await CreateSut().HandleAsync(new GetDataSourceByIdQuery(99), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.NotFound);
    }

    [Fact]
    public async Task Returns_response_when_found()
    {
        var ds = new DataSource
        {
            Id = 7,
            SourceTableName = "ev_recharge_com",
            TableDescription = "EV",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _sources.GetByIdAsync(7L, Arg.Any<CancellationToken>()).Returns(ds);

        var result = await CreateSut().HandleAsync(new GetDataSourceByIdQuery(7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(7);
        result.Value.SourceTableName.Should().Be("ev_recharge_com");
        result.Value.TableDescription.Should().Be("EV");
        result.Value.IsActive.Should().BeTrue();
    }
}
