using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Extensions;
using SalesCom.Application.Commands.Approvals.CreateApprovalFlowLevelUser;
using SalesCom.Application.Commands.Approvals.UpdateApprovalFlowLevelUser;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelUserById;
using SalesCom.Application.Queries.Approvals.GetApprovalFlowLevelUsersByLevelId;
using SalesCom.Application.Queries.Approvals.ListApprovalFlowLevelUsers;
using SalesCom.Application.Queries.Users.LookupUserByLogin;

namespace SalesCom.Api.Controllers
{
    [Route("api/ApprovalFlowLevelUser")]
    [ApiController]
    public class ApprovalFlowLevelUserController : ControllerBase
    {
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        public ApprovalFlowLevelUserController(
            ICommandDispatcher commandDispatcher,
            IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [HttpGet("GetById/{id:long}")]
        public async Task<IActionResult> GetApprovalFlowLevelUserById(long id, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new GetApprovalFlowLevelUserByIdQuery(id), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllApprovalFlowLevelUser(CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new ListApprovalFlowLevelUsersQuery(), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("GetByLevelId/{levelId:long}")]
        public async Task<IActionResult> GetApprovalFlowLevelUsersByLevelId(long levelId, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new GetApprovalFlowLevelUsersByLevelIdQuery(levelId), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpGet("LookupUser")]
        public async Task<IActionResult> LookupUser([FromQuery] string loginName, CancellationToken cancellationToken)
        {
            var result = await _queryDispatcher.DispatchAsync(new LookupUserByLoginQuery(loginName), cancellationToken);
            return result.ToApiResponse(this);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> CreateApprovalFlowLevelUser(
            [FromBody] CreateApprovalFlowLevelUserCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _commandDispatcher.DispatchAsync(command, cancellationToken);
            return result.ToApiResponse(this, "Approver assigned.", StatusCodes.Status201Created);
        }

        [HttpPut("Update/{id:long}")]
        public async Task<IActionResult> UpdateApprovalFlowLevelUser(
            long id,
            [FromBody] UpdateApprovalFlowLevelUserCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _commandDispatcher.DispatchAsync(command with { Id = id }, cancellationToken);
            return result.ToApiResponse(this, "Approver updated.");
        }
    }
}
