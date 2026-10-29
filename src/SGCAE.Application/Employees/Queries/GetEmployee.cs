namespace SGCAE.Application.Employees.Queries;

public sealed record GetEmployeeQuery(Guid Id);

public sealed class GetEmployeeHandler(IEmployeeQueries queries)
{
    public async Task<EmployeeDto> HandleAsync(GetEmployeeQuery query, CancellationToken cancellationToken = default) =>
        await queries.GetByIdAsync(query.Id, cancellationToken)
        ?? throw new EmployeeNotFoundException(query.Id);
}
