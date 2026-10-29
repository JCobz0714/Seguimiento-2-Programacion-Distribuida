using SGCAE.Domain.ValueObjects;

namespace SGCAE.Application.Employees.Commands;

public sealed record UpdateEmployeeCommand(Guid Id, string FirstName, string LastName,
    string Email, string JobTitle, string OrganizationalArea);

public sealed class UpdateEmployeeHandler(IEmployeeRepository repository)
{
    public async Task<EmployeeDto> HandleAsync(UpdateEmployeeCommand command, CancellationToken cancellationToken = default)
    {
        var employee = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new EmployeeNotFoundException(command.Id);
        employee.UpdateDetails(command.FirstName, command.LastName, new EmailAddress(command.Email),
            command.JobTitle, command.OrganizationalArea);
        await repository.SaveChangesAsync(cancellationToken);
        return EmployeeDto.From(employee);
    }
}
