namespace SalesCom.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Extensions;
using SalesCom.Application.Authorization;
using SalesCom.Application.Commands.DataSources.CreateDataSource;
using SalesCom.Application.Commands.DataSources.UpdateDataSource;
using SalesCom.Application.Common;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.DataSources.GetAvailableTableColumns;
using SalesCom.Application.Queries.DataSources.GetDataSourceById;
using SalesCom.Application.Queries.DataSources.ListAvailableTables;
using SalesCom.Application.Queries.DataSources.ListDataSources;
using SalesCom.Infrastructure.Authorization;

[ApiController]
[Route("api/data-sources")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class DataSourcesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    /// <summary>Every available source table (name ends with <c>_COM</c>, case-insensitive) not already registered.</summary>
    [HttpGet("available-tables")]
    //[HasPermission(Permissions.DataSources.View)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AvailableTableResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAvailableTablesAsync(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.DispatchAsync(new ListAvailableTablesQuery(), cancellationToken);
        return result.ToApiResponse(this);
    }

    /// <summary>Read-only column preview of a source table, introspected from the live source database schema.</summary>
    [HttpGet("available-tables/{tableName}/columns")]
    //[HasPermission(Permissions.DataSources.View)]
    [HasRight(Rights.DataSources.View)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AvailableColumnResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableTableColumnsAsync(
        [FromRoute] string tableName,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.DispatchAsync(new GetAvailableTableColumnsQuery(tableName), cancellationToken);
        return result.ToApiResponse(this);
    }

    /// <summary>Paged list of registered data sources.</summary>
    [HttpGet]
    [HasRight(Rights.DataSources.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DataSourceSummary>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.DispatchAsync(new ListDataSourcesQuery(page, pageSize), cancellationToken);
        return result.ToApiResponse(this);
    }

    /// <summary>One registered data source.</summary>
    [HttpGet("{id:guid}")]
    //[HasPermission(Permissions.DataSources.View)]
    /// <summary>One registered data source by id.</summary>
    [HttpGet("{id:long}")]
    [HasRight(Rights.DataSources.View)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.DispatchAsync(new GetDataSourceByIdQuery(id), cancellationToken);
        return result.ToApiResponse(this);
    }

    /// <summary>Registers a source table for commission processing.</summary>
    [HttpPost]
    [HasRight(Rights.DataSources.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);
        return result.ToApiResponse(this, "Data source created.", StatusCodes.Status201Created);
    }

    /// <summary>Updates a registered data source's description and active flag.</summary>
    [HttpPut("{id:long}")]
    [HasRight(Rights.DataSources.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] long id,
        [FromBody] UpdateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.DispatchAsync(command with { Id = id }, cancellationToken);
        return result.ToApiResponse(this, "Data source updated.");
    }
}
