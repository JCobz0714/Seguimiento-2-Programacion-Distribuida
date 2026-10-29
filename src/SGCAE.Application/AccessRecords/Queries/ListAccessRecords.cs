using SGCAE.Domain.Enums;

namespace SGCAE.Application.AccessRecords.Queries;

public sealed record ListAccessRecordsQuery(int Page = 1, int PageSize = 20,
    Guid? EmployeeId = null, Guid? AccessPointId = null, AccessResult? Result = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null);

public sealed class ListAccessRecordsHandler(IAccessRecordQueries queries)
{
    public Task<AccessRecordPage> HandleAsync(ListAccessRecordsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100
            || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw new AccessHistoryValidationException("Página fuera de rango; el tamaño debe estar entre 1 y 100.");
        if (query.EmployeeId == Guid.Empty || query.AccessPointId == Guid.Empty)
            throw new AccessHistoryValidationException("Los identificadores de filtro no pueden estar vacíos.");
        if (query.Result.HasValue && !Enum.IsDefined(query.Result.Value))
            throw new AccessHistoryValidationException("El resultado debe ser Authorized o Rejected.");
        if (query.From.HasValue && query.To.HasValue && query.From.Value >= query.To.Value)
            throw new AccessHistoryValidationException("La fecha from debe ser anterior a to.");

        return queries.ListAsync(query with
        {
            From = query.From?.ToUniversalTime(),
            To = query.To?.ToUniversalTime()
        }, cancellationToken);
    }
}
