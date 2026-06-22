namespace SalesCom.Api.UnitTests.Controllers;

using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Controllers;
using SalesCom.Application.Commands.DataSources.CreateDataSource;
using SalesCom.Application.Commands.DataSources.UpdateDataSource;
using SalesCom.Application.Common;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Application.Queries.DataSources.ListAvailableTables;
using SalesCom.Application.Queries.DataSources.ListDataSources;
using SalesCom.Domain.Common;
using SalesCom.Domain.Errors;

public sealed class DataSourcesControllerTests
{
    private readonly ICommandDispatcher _commands = Substitute.For<ICommandDispatcher>();
    private readonly IQueryDispatcher _queries = Substitute.For<IQueryDispatcher>();

    private DataSourcesController CreateSut() => new(_commands, _queries);

    private static DataSourceResponse Sample(long id = 7) => new(
        Id: id,
        SourceTableName: "ev_recharge_com",
        TableDescription: "primary",
        IsActive: true,
        CreatedOn: DateTimeOffset.UtcNow,
        UpdatedOn: null);

    [Fact]
    public async Task ListAvailableTables_returns_200_with_flat_table_list()
    {
        IReadOnlyList<AvailableTableResponse> tables = new[]
        {
            new AvailableTableResponse("ev_recharge_com"),
            new AvailableTableResponse("dd_target_com"),
        };
        _queries.DispatchAsync(Arg.Any<ListAvailableTablesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(tables));

        var objectResult = (await CreateSut().ListAvailableTablesAsync(CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<AvailableTableResponse>>>()
            .Subject.Data!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAvailableTableColumns_returns_200_with_columns()
    {
        IReadOnlyList<AvailableColumnResponse> columns = new[]
        {
            new AvailableColumnResponse("channel_type", "character varying"),
        };
        _queries.DispatchAsync(Arg.Any<GetAvailableTableColumnsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(columns));

        var actionResult = await CreateSut().GetAvailableTableColumnsAsync("ev_recharge_com", CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
        await _queries.Received(1).DispatchAsync(
            Arg.Is<GetAvailableTableColumnsQuery>(q => q.TableName == "ev_recharge_com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAvailableTableColumns_returns_404_when_table_unknown()
    {
        _queries.DispatchAsync(Arg.Any<GetAvailableTableColumnsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<AvailableColumnResponse>>(DataSourceErrors.SourceTableNotFound));

        var actionResult = await CreateSut().GetAvailableTableColumnsAsync("no_such_table", CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task List_dispatches_query_with_paging_arguments()
    {
        var page = new PagedResult<DataSourceSummary>(new List<DataSourceSummary>(), 1, 25, 0);
        _queries.DispatchAsync(Arg.Any<ListDataSourcesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(page));

        var actionResult = await CreateSut().ListAsync(page: 3, pageSize: 50, cancellationToken: CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
        await _queries.Received(1).DispatchAsync(
            Arg.Is<ListDataSourcesQuery>(q => q.Page == 3 && q.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_returns_200_when_data_source_exists()
    {
        const long id = 7;
        _queries.DispatchAsync(Arg.Any<GetDataSourceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Sample(id)));

        var actionResult = await CreateSut().GetAsync(id, CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
        await _queries.Received(1).DispatchAsync(
            Arg.Is<GetDataSourceByIdQuery>(q => q.Id == id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_returns_404_when_missing()
    {
        _queries.DispatchAsync(Arg.Any<GetDataSourceByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DataSourceResponse>(DataSourceErrors.NotFound));

        var actionResult = await CreateSut().GetAsync(99, CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Create_returns_201_and_dispatches_the_bound_command()
    {
        _commands.DispatchAsync(Arg.Any<CreateDataSourceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Sample()));

        var command = new CreateDataSourceCommand("ev_recharge_com", "primary", true);

        var actionResult = await CreateSut().CreateAsync(command, CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        await _commands.Received(1).DispatchAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_returns_409_when_already_registered()
    {
        _commands.DispatchAsync(Arg.Any<CreateDataSourceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DataSourceResponse>(DataSourceErrors.AlreadyRegistered));

        var actionResult = await CreateSut().CreateAsync(
            new CreateDataSourceCommand("ev_recharge_com", null, true), CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Update_dispatches_command_with_the_route_id_merged_in()
    {
        const long id = 7;
        _commands.DispatchAsync(Arg.Any<UpdateDataSourceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Sample(id)));

        // Id on the bound body is ignored; the controller merges the route id in.
        var command = new UpdateDataSourceCommand(0, "edited", false);

        var actionResult = await CreateSut().UpdateAsync(id, command, CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(200);
        await _commands.Received(1).DispatchAsync(
            Arg.Is<UpdateDataSourceCommand>(c => c.Id == id && c.TableDescription == "edited" && !c.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_returns_404_when_data_source_missing()
    {
        _commands.DispatchAsync(Arg.Any<UpdateDataSourceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DataSourceResponse>(DataSourceErrors.NotFound));

        var actionResult = await CreateSut().UpdateAsync(99, new UpdateDataSourceCommand(0, null, true), CancellationToken.None);

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
    }
}
