using SGCAE.Application.Employees;
using SGCAE.Domain.Common;
using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Application.Credentials.Commands;

public sealed record AssignCredentialCommand(Guid EmployeeId, string Code);

public sealed class AssignCredentialHandler(ICredentialRepository repository)
{
    public Task<CredentialDto> HandleAsync(AssignCredentialCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EmployeeId == Guid.Empty)
            throw new DomainException("El identificador del empleado es obligatorio.");
        var code = new CredentialCode(command.Code);
        return repository.ExecuteAsync(async token =>
        {
            var employee = await repository.GetEmployeeForUpdateAsync(command.EmployeeId, token)
                ?? throw new EmployeeNotFoundException(command.EmployeeId);
            var credential = new Credential(employee, code);
            repository.Add(credential);
            await repository.SaveChangesAsync(token);
            return CredentialDto.From(credential);
        }, cancellationToken);
    }
}
