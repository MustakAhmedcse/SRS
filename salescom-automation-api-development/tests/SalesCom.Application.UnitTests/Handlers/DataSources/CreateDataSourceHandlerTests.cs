namespace SalesCom.Application.UnitTests.Handlers.DataSources;

using System.Linq.Expressions;
using SalesCom.Application.Commands.DataSources.CreateDataSource;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

public sealed class CreateDataSourceHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<DataSource> _sources = Substitute.For<IGenericRepository<DataSource>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public CreateDataSourceHandlerTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
        _currentUser.UserName.Returns("alice");
        _unitOfWork.Repository<DataSource>().Returns(_sources);
    }

    private CreateDataSourceHandler CreateSut() => new(_unitOfWork, _currentUser, _clock);

    private static CreateDataSourceCommand Cmd(string table = "ev_recharge_com") =>
        new(table, "EV Recharge", IsActive: true);

    [Fact]
    public async Task Persists_data_source_and_returns_response()
    {
        _sources.AnyAsync(Arg.Any<Expression<Func<DataSource, bool>>>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateSut().HandleAsync(Cmd(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceTableName.Should().Be("ev_recharge_com");
        result.Value.TableDescription.Should().Be("EV Recharge");
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedOn.Should().Be(_clock.UtcNow);

        await _sources.Received(1).AddAsync(
            Arg.Is<DataSource>(d => d.SourceTableName == "ev_recharge_com" && d.CreatedBy == "alice"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_AlreadyRegistered_when_source_table_is_taken()
    {
        _sources.AnyAsync(Arg.Any<Expression<Func<DataSource, bool>>>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().HandleAsync(Cmd(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.AlreadyRegistered);
        await _sources.DidNotReceive().AddAsync(Arg.Any<DataSource>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Trims_table_and_description()
    {
        _sources.AnyAsync(Arg.Any<Expression<Func<DataSource, bool>>>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateSut().HandleAsync(
            new CreateDataSourceCommand("  ev_recharge_com  ", "  desc  ", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceTableName.Should().Be("ev_recharge_com");
        result.Value.TableDescription.Should().Be("desc");
    }
}
