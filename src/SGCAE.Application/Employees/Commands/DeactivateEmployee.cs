namespace SGCAE.Application.Employees.Commands;

public sealed record DeactivateEmployeeCommand(Guid Id);

public sealed class DeactivateEmployeeHandler(IEmployeeRepository repository)
{
    public async Task HandleAsync(DeactivateEmployeeCommand command, CancellationToken cancellationToken = default)
    {
        var employee = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new EmployeeNotFoundException(command.Id);
        employee.Deactivate();
        await repository.SaveChangesAsync(cancellationToken);
    }
}
