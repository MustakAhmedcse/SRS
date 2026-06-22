namespace SalesCom.Api.IntegrationTests.Endpoints;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SalesCom.Api.IntegrationTests.Infrastructure;
using SalesCom.Application.Common;
using SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Application.Queries.DataSources.ListAvailableTables;
using SalesCom.Application.Queries.DataSources.ListDataSources;

[Collection(SalesComCollection.Name)]
public sealed class DataSourcesEndpointsTests
{
    private readonly HttpClient _client;

    public DataSourcesEndpointsTests(SalesComFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
    }

    [Fact]
    public async Task Available_tables_returns_flat_list_on_fresh_db()
    {
        var response = await _client.GetAsync("/api/data-sources/available-tables");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AvailableTableResponse>>>();
        body!.Success.Should().BeTrue();
        body.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_columns_for_unknown_table_returns_404()
    {
        var response = await _client.GetAsync("/api/data-sources/available-tables/no_such_table_com/columns");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.ErrorCode.Should().Be("DataSource.SourceTableNotFound");
    }

    [Fact]
    public async Task Available_columns_for_a_real_table_returns_its_columns()
    {
        // Exercises uow.QueryAsync<AvailableColumnResponse> against information_schema with real rows.
        var response = await _client.GetAsync("/api/data-sources/available-tables/users/columns");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AvailableColumnResponse>>>();
        body!.Success.Should().BeTrue();
        body.Data!.Should().NotBeEmpty();
        body.Data!.Select(c => c.ColumnName).Should().Contain("user_name");
    }

    [Fact]
    public async Task List_returns_paged_envelope()
    {
        var response = await _client.GetAsync("/api/data-sources?page=1&pageSize=25");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<PagedResult<DataSourceSummary>>>();
        body!.Success.Should().BeTrue();
        body.Data!.Page.Should().Be(1);
        body.Data.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404()
    {
        var response = await _client.GetAsync("/api/data-sources/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.ErrorCode.Should().Be("DataSource.NotFound");
    }

    [Fact]
    public async Task Create_with_empty_source_table_returns_400_validation_error()
    {
        var response = await _client.PostAsJsonAsync("/api/data-sources", new
        {
            sourceTableName = "",
            tableDescription = (string?)null,
            isActive = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_then_get_then_update_round_trip()
    {
        var sourceTable = $"int_test_{Guid.NewGuid():N}_com";

        var create = await _client.PostAsJsonAsync("/api/data-sources", new
        {
            sourceTableName = sourceTable,
            tableDescription = "round-trip",
            isActive = true,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<ApiResponseDto<DataSourceResponse>>();
        created!.Success.Should().BeTrue();
        created.Data!.SourceTableName.Should().Be(sourceTable);
        created.Data.TableDescription.Should().Be("round-trip");
        created.Data.IsActive.Should().BeTrue();

        var fetched = await _client.GetFromJsonAsync<ApiResponseDto<DataSourceResponse>>($"/api/data-sources/{created.Data.Id}");
        fetched!.Data!.SourceTableName.Should().Be(sourceTable);
        fetched.Data.TableDescription.Should().Be("round-trip");

        var update = await _client.PutAsJsonAsync($"/api/data-sources/{created.Data.Id}", new
        {
            tableDescription = "after edit",
            isActive = false,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<ApiResponseDto<DataSourceResponse>>();
        updated!.Data!.TableDescription.Should().Be("after edit");
        updated.Data.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Create_duplicate_source_table_returns_409()
    {
        var sourceTable = $"int_dup_{Guid.NewGuid():N}_com";
        var body = new { sourceTableName = sourceTable, tableDescription = (string?)null, isActive = true };

        (await _client.PostAsJsonAsync("/api/data-sources", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/data-sources", body);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await second.Content.ReadFromJsonAsync<ApiResponseDto>();
        conflict!.ErrorCode.Should().Be("DataSource.AlreadyRegistered");
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var response = await _client.PutAsJsonAsync("/api/data-sources/999999", new
        {
            tableDescription = (string?)null,
            isActive = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.ErrorCode.Should().Be("DataSource.NotFound");
    }
}
