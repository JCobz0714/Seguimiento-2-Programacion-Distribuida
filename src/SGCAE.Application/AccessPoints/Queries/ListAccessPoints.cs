namespace SGCAE.Application.AccessPoints.Queries;

public sealed record ListAccessPointsQuery(int Page = 1, int PageSize = 20, bool? IsEnabled = null);

public sealed class ListAccessPointsHandler(IAccessPointQueries queries)
{
    public async Task<AccessPointPage> HandleAsync(ListAccessPointsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
            throw new AccessPointQueryValidationException("La página debe ser mayor que cero.");
        if (query.PageSize is < 1 or > 100)
            throw new AccessPointQueryValidationException("El tamaño de página debe estar entre 1 y 100.");
        if (((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw new AccessPointQueryValidationException("La página solicitada supera el límite admitido.");

        return await queries.ListAsync(query.Page, query.PageSize, query.IsEnabled, cancellationToken);
    }
}
