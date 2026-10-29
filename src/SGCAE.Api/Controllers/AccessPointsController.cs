using Microsoft.AspNetCore.Mvc;
using SGCAE.Api.Contracts;
using SGCAE.Application.AccessPoints;
using SGCAE.Application.AccessPoints.Commands;
using SGCAE.Application.AccessPoints.Queries;

namespace SGCAE.Api.Controllers;

[ApiController]
[Route("api/access-points")]
public sealed class AccessPointsController(
    CreateAccessPointHandler create,
    GetAccessPointHandler get,
    ListAccessPointsHandler list) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccessPointDto>> Create(CreateAccessPointRequest request, CancellationToken cancellationToken)
    {
        var point = await create.HandleAsync(new CreateAccessPointCommand(request.Name, request.IsEnabled), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = point.Id }, point);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccessPointDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessPointDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await get.HandleAsync(new GetAccessPointQuery(id), cancellationToken));

    [HttpGet]
    [ProducesResponseType<AccessPointPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessPointPage>> List(CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isEnabled = null) =>
        Ok(await list.HandleAsync(new ListAccessPointsQuery(page, pageSize, isEnabled), cancellationToken));
}
