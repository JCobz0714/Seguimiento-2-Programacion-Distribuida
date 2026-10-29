namespace SGCAE.Application.Credentials.Queries;

public sealed record GetCredentialQuery(Guid Id);

public sealed class GetCredentialHandler(ICredentialQueries queries)
{
    public async Task<CredentialDto> HandleAsync(GetCredentialQuery query, CancellationToken cancellationToken = default) =>
        await queries.GetByIdAsync(query.Id, cancellationToken)
        ?? throw new CredentialNotFoundException(query.Id);
}
