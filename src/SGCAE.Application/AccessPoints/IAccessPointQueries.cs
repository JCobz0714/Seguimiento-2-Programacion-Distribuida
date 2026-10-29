namespace SGCAE.Application.AccessPoints;

public interface IAccessPointQueries
{
    Task<AccessPointDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AccessPointPage> ListAsync(int page, int pageSize, bool? isEnabled, CancellationToken cancellationToken);
}
