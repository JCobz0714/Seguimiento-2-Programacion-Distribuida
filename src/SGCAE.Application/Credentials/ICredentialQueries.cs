using SGCAE.Domain.Enums;

namespace SGCAE.Application.Credentials;

public interface ICredentialQueries
{
    Task<CredentialDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CredentialPage> ListAsync(int page, int pageSize, Guid? employeeId, CredentialStatus? status,
        CancellationToken cancellationToken);
}
