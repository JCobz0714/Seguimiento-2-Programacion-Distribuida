using SGCAE.Domain.Entities;

namespace SGCAE.Application.AccessPoints;

public interface IAccessPointRepository
{
    void Add(AccessPoint accessPoint);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
