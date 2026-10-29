using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Application.AccessRecords;

public interface IAccessRecordRepository
{
    // Mantiene consistentes las lecturas de la política hasta confirmar el registro.
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task<Credential?> GetCredentialAsync(CredentialCode code, CancellationToken cancellationToken);
    Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken cancellationToken);
    Task<AccessPoint?> GetAccessPointAsync(Guid id, CancellationToken cancellationToken);
    void Add(AccessRecord record);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
