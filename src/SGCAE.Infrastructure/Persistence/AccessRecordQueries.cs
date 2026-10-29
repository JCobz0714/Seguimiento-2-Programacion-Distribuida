using System.Data;
using Microsoft.EntityFrameworkCore;
using SGCAE.Application.AccessRecords;
using SGCAE.Application.AccessRecords.Queries;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class AccessRecordQueries(SgcaeDbContext context) : IAccessRecordQueries
{
    public Task<AccessRecordPage> ListAsync(ListAccessRecordsQuery request, CancellationToken cancellationToken) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Evita que un INSERT entre Count y la página produzca una respuesta inconsistente.
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var query = context.AccessRecords.AsNoTracking();
            if (request.EmployeeId.HasValue)
                query = query.Where(r => r.EmployeeId == request.EmployeeId.Value);
            if (request.AccessPointId.HasValue)
                query = query.Where(r => r.RequestedAccessPointId == request.AccessPointId.Value);
            if (request.Result.HasValue)
                query = query.Where(r => r.Result == request.Result.Value);
            if (request.From.HasValue)
                query = query.Where(r => r.OccurredAt >= request.From.Value);
            if (request.To.HasValue)
                query = query.Where(r => r.OccurredAt < request.To.Value);

            var totalCount = await query.CountAsync(cancellationToken);
            var records = await query.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Id)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
                .ToListAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AccessRecordPage(records.Select(AccessRecordDto.From).ToArray(),
                request.Page, request.PageSize, totalCount);
        });
}
