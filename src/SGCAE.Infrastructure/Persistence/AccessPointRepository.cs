using SGCAE.Application.AccessPoints;
using SGCAE.Domain.Entities;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class AccessPointRepository(SgcaeDbContext context) : IAccessPointRepository
{
    public void Add(AccessPoint accessPoint) => context.AccessPoints.Add(accessPoint);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
