using Microsoft.EntityFrameworkCore;
using SGCAE.Application.Employees;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class EmployeeQueries(SgcaeDbContext context) : IEmployeeQueries
{
    public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.AsNoTracking()
            .SingleOrDefaultAsync(employee => employee.Id == id, cancellationToken);
        return employee is null ? null : EmployeeDto.From(employee);
    }

    public async Task<EmployeePage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.Employees.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var employees = await query.OrderBy(employee => employee.Identification).ThenBy(employee => employee.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new EmployeePage(employees.Select(EmployeeDto.From).ToArray(), page, pageSize, totalCount);
    }
}
