using System.Data;
using Microsoft.EntityFrameworkCore;
using SGCAE.Application.AccessRecords;
using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class AccessRecordRepository(SgcaeDbContext context) : IAccessRecordRepository
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

    public Task<Credential?> GetCredentialAsync(CredentialCode code, CancellationToken cancellationToken) =>
        context.Credentials.AsNoTracking().SingleOrDefaultAsync(c => c.Code == code, cancellationToken);

    public Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<AccessPoint?> GetAccessPointAsync(Guid id, CancellationToken cancellationToken) =>
        context.AccessPoints.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(AccessRecord record) => context.AccessRecords.Add(record);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
