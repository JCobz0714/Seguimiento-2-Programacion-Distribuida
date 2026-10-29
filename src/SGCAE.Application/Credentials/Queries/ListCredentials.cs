using SGCAE.Domain.Enums;

namespace SGCAE.Application.Credentials.Queries;

public sealed record ListCredentialsQuery(int Page = 1, int PageSize = 20,
    Guid? EmployeeId = null, CredentialStatus? Status = null);

public sealed class ListCredentialsHandler(ICredentialQueries queries)
{
    public Task<CredentialPage> HandleAsync(ListCredentialsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100
            || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw new CredentialQueryValidationException("Página fuera de rango; el tamaño debe estar entre 1 y 100.");
        if (query.EmployeeId == Guid.Empty)
            throw new CredentialQueryValidationException("El identificador del empleado no puede estar vacío.");
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new CredentialQueryValidationException("El estado de credencial no es válido.");
        return queries.ListAsync(query.Page, query.PageSize, query.EmployeeId, query.Status, cancellationToken);
    }
}
