using Microsoft.AspNetCore.Mvc;
using SGCAE.Api.Contracts;
using SGCAE.Application.Employees;
using SGCAE.Application.Employees.Commands;
using SGCAE.Application.Employees.Queries;

namespace SGCAE.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(
    CreateEmployeeHandler create,
    UpdateEmployeeHandler update,
    DeactivateEmployeeHandler deactivate,
    GetEmployeeHandler get,
    ListEmployeesHandler list) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await create.HandleAsync(new CreateEmployeeCommand(request.Identification,
            request.FirstName, request.LastName, request.Email, request.JobTitle, request.OrganizationalArea),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await get.HandleAsync(new GetEmployeeQuery(id), cancellationToken));

    [HttpGet]
    [ProducesResponseType<EmployeePage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeePage>> List(CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await list.HandleAsync(new ListEmployeesQuery(page, pageSize), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken) =>
        Ok(await update.HandleAsync(new UpdateEmployeeCommand(id, request.FirstName, request.LastName,
            request.Email, request.JobTitle, request.OrganizationalArea), cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await deactivate.HandleAsync(new DeactivateEmployeeCommand(id), cancellationToken);
        return NoContent();
    }
}
