namespace SalesCom.Application.UnitTests.Handlers.DataSources;

using System.Linq.Expressions;
using SalesCom.Application.Queries.DataSources.ListDataSources;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Interfaces;

public sealed class ListDataSourcesHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<DataSource> _sources = Substitute.For<IGenericRepository<DataSource>>();

    public ListDataSourcesHandlerTests()
    {
        _unitOfWork.Repository<DataSource>().Returns(_sources);
    }

    private ListDataSourcesHandler CreateSut() => new(_unitOfWork);

    private static DataSource Make(string name) => new()
    {
        SourceTableName = name,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private void PageReturns(IReadOnlyList<DataSource> items, int total)
    {
        _sources.PagedAsync(
                Arg.Any<Expression<Func<DataSource, bool>>>(),
                Arg.Any<Func<IQueryable<DataSource>, IOrderedQueryable<DataSource>>>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(items);
        _sources.CountAsync(Arg.Any<Expression<Func<DataSource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(total);
    }

    [Fact]
    public async Task Returns_paged_summary()
    {
        PageReturns(new List<DataSource> { Make("a_com"), Make("b_com") }, 2);

        var result = await CreateSut().HandleAsync(new ListDataSourcesQuery(1, 25), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task Clamps_page_and_page_size()
    {
        PageReturns(new List<DataSource>(), 0);

        var result = await CreateSut().HandleAsync(new ListDataSourcesQuery(-1, 9999), CancellationToken.None);

        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(200);
        await _sources.Received(1).PagedAsync(
            Arg.Any<Expression<Func<DataSource, bool>>>(),
            Arg.Any<Func<IQueryable<DataSource>, IOrderedQueryable<DataSource>>>(),
            0, 200, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Calculates_skip_from_page_and_page_size()
    {
        PageReturns(new List<DataSource>(), 0);

        await CreateSut().HandleAsync(new ListDataSourcesQuery(3, 25), CancellationToken.None);

        await _sources.Received(1).PagedAsync(
            Arg.Any<Expression<Func<DataSource, bool>>>(),
            Arg.Any<Func<IQueryable<DataSource>, IOrderedQueryable<DataSource>>>(),
            50, 25, Arg.Any<CancellationToken>());
    }
}
