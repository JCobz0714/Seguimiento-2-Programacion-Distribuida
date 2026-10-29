using Microsoft.AspNetCore.Mvc;
using SGCAE.Api.Contracts;
using SGCAE.Application.AccessRecords;
using SGCAE.Application.AccessRecords.Commands;
using SGCAE.Application.AccessRecords.Queries;
using SGCAE.Domain.Enums;

namespace SGCAE.Api.Controllers;

[ApiController]
[Route("api/access-records")]
public sealed class AccessRecordsController(RegisterAccessHandler register, ListAccessRecordsHandler list) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccessRecordPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessRecordPage>> List(CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? employeeId = null, [FromQuery] Guid? accessPointId = null,
        [FromQuery] AccessResult? result = null,
        [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null) =>
        Ok(await list.HandleAsync(new ListAccessRecordsQuery(page, pageSize,
            employeeId, accessPointId, result, from, to), cancellationToken));

    [HttpPost]
    [ProducesResponseType<AccessRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccessRecordDto>> Register(RegisterAccessRequest request, CancellationToken cancellationToken)
    {
        var record = await register.HandleAsync(new RegisterAccessCommand(
            request.CredentialCode, request.AccessPointId, request.Movement), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, record);
    }
}
