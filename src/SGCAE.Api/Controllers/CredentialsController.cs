using Microsoft.AspNetCore.Mvc;
using SGCAE.Api.Contracts;
using SGCAE.Application.Credentials;
using SGCAE.Application.Credentials.Commands;
using SGCAE.Application.Credentials.Queries;
using SGCAE.Domain.Enums;

namespace SGCAE.Api.Controllers;

[ApiController]
[Route("api/credentials")]
public sealed class CredentialsController(AssignCredentialHandler assign, BlockCredentialHandler block,
    RevokeCredentialHandler revoke, GetCredentialHandler get, ListCredentialsHandler list) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CredentialDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CredentialDto>> Assign(AssignCredentialRequest request, CancellationToken cancellationToken)
    {
        var credential = await assign.HandleAsync(new AssignCredentialCommand(request.EmployeeId, request.Code), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = credential.Id }, credential);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CredentialDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await get.HandleAsync(new GetCredentialQuery(id), cancellationToken));

    [HttpGet]
    public async Task<ActionResult<CredentialPage>> List(CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? employeeId = null, [FromQuery] CredentialStatus? status = null) =>
        Ok(await list.HandleAsync(new ListCredentialsQuery(page, pageSize, employeeId, status), cancellationToken));

    [HttpPatch("{id:guid}/block")]
    public async Task<ActionResult<CredentialDto>> Block(Guid id, CancellationToken cancellationToken) =>
        Ok(await block.HandleAsync(new BlockCredentialCommand(id), cancellationToken));

    [HttpPatch("{id:guid}/revoke")]
    public async Task<ActionResult<CredentialDto>> Revoke(Guid id, CancellationToken cancellationToken) =>
        Ok(await revoke.HandleAsync(new RevokeCredentialCommand(id), cancellationToken));
}
