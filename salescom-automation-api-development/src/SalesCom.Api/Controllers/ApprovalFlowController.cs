using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Extensions;
using SalesCom.Application.Commands.Approvals.CreateApprovalFlow;
using SalesCom.Application.Commands.Approvals.UpdateApprovalFlow;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.Approvals.GetApprovalFlowById;
using SalesCom.Application.Queries.Approvals.ListApprovalFlows;

namespace SalesCom.Api.Controllers
{
    [Route("api/ApprovalFlow")]
    [ApiController]
    public class ApprovalFlowController : ControllerBase
    {
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        public ApprovalFlowController(
            ICommandDispatcher commandDispatcher,
            IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [HttpGet("GetById{id:long}")]
        public async Task<IActionResult> GetApprovalFlowById(long id, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new GetApprovalFlowByIdQuery(id), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllApprovalFlow(CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new ListApprovalFlowsQuery(), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> CreateApprovalFlow(
            [FromBody] CreateApprovalFlowCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _commandDispatcher.DispatchAsync(command, cancellationToken);
            return result.ToApiResponse(this, "Approval flow created.", StatusCodes.Status201Created);
        }

        [HttpPut("Update/{id:long}")]
        public async Task<IActionResult> UpdateApprovalFlow(
            long id,
            [FromBody] UpdateApprovalFlowCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _commandDispatcher.DispatchAsync(command with { Id = id }, cancellationToken);
            return result.ToApiResponse(this, "Approval flow updated.");
        }
    }
}
