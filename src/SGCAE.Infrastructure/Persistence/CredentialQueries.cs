using Microsoft.EntityFrameworkCore;
using SGCAE.Application.Credentials;
using SGCAE.Domain.Enums;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class CredentialQueries(SgcaeDbContext context) : ICredentialQueries
{
    public async Task<CredentialDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var credential = await context.Credentials.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        return credential is null ? null : CredentialDto.From(credential);
    }

    public async Task<CredentialPage> ListAsync(int page, int pageSize, Guid? employeeId,
        CredentialStatus? status, CancellationToken cancellationToken)
    {
        var query = context.Credentials.AsNoTracking();
        if (employeeId.HasValue) query = query.Where(c => c.EmployeeId == employeeId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.Code).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new CredentialPage(items.Select(CredentialDto.From).ToArray(), page, pageSize, total);
    }
}
