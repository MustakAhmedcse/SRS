namespace SalesCom.Application.UnitTests.Handlers.DataSources;

using SalesCom.Application.Commands.DataSources.UpdateDataSource;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Entities.DataSources;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

public sealed class UpdateDataSourceHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<DataSource> _sources = Substitute.For<IGenericRepository<DataSource>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private const long DataSourceId = 7;
    private readonly DataSource _dataSource;

    public UpdateDataSourceHandlerTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
        _currentUser.UserName.Returns("bob");
        _dataSource = new DataSource
        {
            Id = DataSourceId,
            SourceTableName = "ev_recharge_com",
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = "alice",
        };
        _unitOfWork.Repository<DataSource>().Returns(_sources);
        _sources.GetByIdAsync(DataSourceId, Arg.Any<CancellationToken>()).Returns(_dataSource);
    }

    private UpdateDataSourceHandler CreateSut() => new(_unitOfWork, _currentUser, _clock);

    [Fact]
    public async Task Returns_NotFound_when_data_source_is_missing()
    {
        _sources.GetByIdAsync(DataSourceId, Arg.Any<CancellationToken>()).Returns((DataSource?)null);

        var result = await CreateSut().HandleAsync(
            new UpdateDataSourceCommand(DataSourceId, "edited", true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.NotFound);
        await _sources.DidNotReceive().UpdateAsync(Arg.Any<DataSource>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updates_description_active_flag_and_audit_fields_in_one_save()
    {
        var result = await CreateSut().HandleAsync(
            new UpdateDataSourceCommand(DataSourceId, "  edited  ", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _dataSource.TableDescription.Should().Be("edited");
        _dataSource.IsActive.Should().BeFalse();
        _dataSource.UpdatedAt.Should().Be(_clock.UtcNow);
        _dataSource.UpdatedBy.Should().Be("bob");
        await _sources.Received(1).UpdateAsync(_dataSource, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).Commit(Arg.Any<CancellationToken>());
    }
}
