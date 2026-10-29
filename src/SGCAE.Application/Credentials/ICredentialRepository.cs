using SGCAE.Domain.Entities;

namespace SGCAE.Application.Credentials;

public interface ICredentialRepository
{
    // Ejecuta el comando completo en una transacción; las lecturas bloquean hasta guardar.
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task<Employee?> GetEmployeeForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<Credential?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);
    void Add(Credential credential);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
