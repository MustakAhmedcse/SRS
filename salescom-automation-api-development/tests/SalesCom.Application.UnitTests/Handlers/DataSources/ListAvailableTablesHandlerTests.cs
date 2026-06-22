namespace SalesCom.Application.UnitTests.Handlers.DataSources;

using System.Linq.Expressions;
using SalesCom.Application.Queries.DataSources.ListAvailableTables;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Interfaces;

public sealed class ListAvailableTablesHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<DataSource> _sources = Substitute.For<IGenericRepository<DataSource>>();

    public ListAvailableTablesHandlerTests()
    {
        _unitOfWork.Repository<DataSource>().Returns(_sources);
    }

    private ListAvailableTablesHandler CreateSut() => new(_unitOfWork);

    private void CatalogReturns(params string[] tables) =>
        _unitOfWork.QueryAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object[]>())
            .Returns(tables.ToList());

    private void Registered(params string[] names) =>
        _sources.ListAsync(Arg.Any<Expression<Func<DataSource, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(names.Select(n => new DataSource { SourceTableName = n, CreatedAt = DateTimeOffset.UtcNow }).ToList());

    [Fact]
    public async Task Returns_all_tables_when_none_are_registered()
    {
        CatalogReturns("ev_recharge_com", "dd_target_com");
        Registered();

        var result = await CreateSut().HandleAsync(new ListAvailableTablesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.TableName).Should().Equal("ev_recharge_com", "dd_target_com");
    }

    [Fact]
    public async Task Excludes_tables_already_registered_as_data_sources()
    {
        CatalogReturns("ev_recharge_com", "dd_target_com", "sc_sale_com");
        Registered("ev_recharge_com");

        var result = await CreateSut().HandleAsync(new ListAvailableTablesQuery(), CancellationToken.None);

        result.Value.Select(t => t.TableName).Should().BeEquivalentTo(new[] { "dd_target_com", "sc_sale_com" });
    }

    [Fact]
    public async Task Filter_is_case_insensitive()
    {
        CatalogReturns("EV_RECHARGE_COM", "dd_target_com");
        Registered("ev_recharge_com");

        var result = await CreateSut().HandleAsync(new ListAvailableTablesQuery(), CancellationToken.None);

        result.Value.Should().HaveCount(1);
        result.Value[0].TableName.Should().Be("dd_target_com");
    }

    [Fact]
    public async Task Empty_catalog_returns_empty_list()
    {
        CatalogReturns();
        Registered();

        var result = await CreateSut().HandleAsync(new ListAvailableTablesQuery(), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }
}
