using SGCAE.Domain.Entities;

namespace SGCAE.Application.Employees;

// Las entidades cargadas para comandos mantienen seguimiento hasta guardar.
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(Employee employee);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
