using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Application.Employees.Commands;

public sealed record CreateEmployeeCommand(string Identification, string FirstName,
    string LastName, string Email, string JobTitle, string OrganizationalArea);

public sealed class CreateEmployeeHandler(IEmployeeRepository repository)
{
    public async Task<EmployeeDto> HandleAsync(CreateEmployeeCommand command, CancellationToken cancellationToken = default)
    {
        var employee = new Employee(new EmployeeIdentification(command.Identification),
            command.FirstName, command.LastName, new EmailAddress(command.Email),
            command.JobTitle, command.OrganizationalArea);
        repository.Add(employee);
        await repository.SaveChangesAsync(cancellationToken);
        return EmployeeDto.From(employee);
    }
}
