using Microsoft.EntityFrameworkCore;
using SGCAE.Application.AccessPoints;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class AccessPointQueries(SgcaeDbContext context) : IAccessPointQueries
{
    public Task<AccessPointDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.AccessPoints.AsNoTracking().Where(point => point.Id == id)
            .Select(point => new AccessPointDto(point.Id, point.Name, point.IsEnabled))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AccessPointPage> ListAsync(int page, int pageSize, bool? isEnabled, CancellationToken cancellationToken)
    {
        var query = context.AccessPoints.AsNoTracking();
        if (isEnabled.HasValue)
            query = query.Where(point => point.IsEnabled == isEnabled.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(point => point.Name).ThenBy(point => point.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(point => new AccessPointDto(point.Id, point.Name, point.IsEnabled))
            .ToListAsync(cancellationToken);
        return new AccessPointPage(items, page, pageSize, totalCount);
    }
}
