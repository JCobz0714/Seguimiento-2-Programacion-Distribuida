using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGCAE.Application.Employees;
using SGCAE.Domain.Entities;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class EmployeeRepository(SgcaeDbContext context) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Employees.SingleOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public void Add(Employee employee) => context.Employees.Add(employee);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                error.Number is 2601 or 2627
                && error.Message.Contains("IX_Employees_Identification", StringComparison.Ordinal)))
        {
            // La restricción cubre también solicitudes concurrentes; otros errores SQL se propagan.
            throw new DuplicateEmployeeIdentificationException(exception);
        }
    }
}
