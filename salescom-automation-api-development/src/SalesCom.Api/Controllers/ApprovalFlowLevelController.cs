using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Extensions;
using SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevel;
using SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevel;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelById;
using SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelsByFlowId;
using SalesCom.Application.Queries.Approvals.GetApprovalFlowState;
using SalesCom.Application.Queries.Approvals.ListApprovalFlowLevels;
using SalesCom.Application.Queries.Approvals.ListApprovalTypes;

namespace SalesCom.Api.Controllers
{
    [Route("api/ApprovalFlowLevel")]
    [ApiController]
    public class ApprovalFlowLevelController : ControllerBase
    {
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        public ApprovalFlowLevelController(
            ICommandDispatcher commandDispatcher,
            IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [HttpGet("GetById/{id:long}")]
        public async Task<IActionResult> GetApprovalFlowLevelById(long id, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new GetApprovalFlowLevelByIdQuery(id), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllApprovalFlowLevel(CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new ListApprovalFlowLevelsQuery(), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("GetByFlowId/{flowId:long}")]
        public async Task<IActionResult> GetApprovalFlowLevelsByFlowId(long flowId, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new GetApprovalFlowLevelsByFlowIdQuery(flowId), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("FlowState/{flowId:long}")]
        public async Task<IActionResult> GetApprovalFlowState(long flowId, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new GetApprovalFlowStateQuery(flowId), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> CreateApprovalFlowLevel(
            [FromBody] CreateApprovalFlowLevelCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _commandDispatcher.DispatchAsync(command, cancellationToken);
            return result.ToApiResponse(this, "Approval flow level created.", StatusCodes.Status201Created);
        }

        [HttpPut("Update/{id:long}")]
        public async Task<IActionResult> UpdateApprovalFlowLevel(
            long id,
            [FromBody] UpdateApprovalFlowLevelCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _commandDispatcher.DispatchAsync(command with { Id = id }, cancellationToken);
            return result.ToApiResponse(this, "Approval flow level updated.");
        }

        [HttpGet("GetAllApprovalType")]
        public async Task<IActionResult> GetAllApprovalType(CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new ListApprovalTypesQuery(), cancellationToken);
            return result.ToApiResponse(this);
        }
    }
}
