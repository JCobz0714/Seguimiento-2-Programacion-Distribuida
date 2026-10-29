namespace SGCAE.Application.Employees.Queries;

public sealed record ListEmployeesQuery(int Page = 1, int PageSize = 20);

public sealed class ListEmployeesHandler(IEmployeeQueries queries)
{
    public async Task<EmployeePage> HandleAsync(ListEmployeesQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100
            || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw new EmployeeQueryValidationException(
                "La página debe ser positiva, el tamaño debe estar entre 1 y 100 y el desplazamiento no debe exceder Int32.");
        return await queries.ListAsync(query.Page, query.PageSize, cancellationToken);
    }
}
