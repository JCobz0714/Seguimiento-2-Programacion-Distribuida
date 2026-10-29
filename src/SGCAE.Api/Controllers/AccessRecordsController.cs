using Microsoft.AspNetCore.Mvc;
using SGCAE.Api.Contracts;
using SGCAE.Application.AccessRecords;
using SGCAE.Application.AccessRecords.Commands;

namespace SGCAE.Api.Controllers;

[ApiController]
[Route("api/access-records")]
public sealed class AccessRecordsController(RegisterAccessHandler register) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AccessRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccessRecordDto>> Register(RegisterAccessRequest request, CancellationToken cancellationToken)
    {
        var record = await register.HandleAsync(new RegisterAccessCommand(
            request.CredentialCode, request.AccessPointId, request.Movement), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, record);
    }
}
