namespace SGCAE.Application.Employees;

// Las lecturas devuelven DTO y no permiten modificar entidades.
public interface IEmployeeQueries
{
    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeePage> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
