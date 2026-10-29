namespace SGCAE.Application.AccessPoints.Queries;

public sealed record GetAccessPointQuery(Guid Id);

public sealed class GetAccessPointHandler(IAccessPointQueries queries)
{
    public async Task<AccessPointDto> HandleAsync(GetAccessPointQuery query, CancellationToken cancellationToken = default) =>
        await queries.GetByIdAsync(query.Id, cancellationToken)
        ?? throw new AccessPointNotFoundException(query.Id);
}
