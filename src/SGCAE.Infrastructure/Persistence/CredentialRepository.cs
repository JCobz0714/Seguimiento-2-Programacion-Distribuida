using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGCAE.Application.Credentials;
using SGCAE.Domain.Entities;

namespace SGCAE.Infrastructure.Persistence;

internal sealed class CredentialRepository(SgcaeDbContext context) : ICredentialRepository
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Un reintento debe volver a leer el estado confirmado por SQL Server.
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

    public Task<Employee?> GetEmployeeForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Employees.FromSqlInterpolated(
            $"SELECT * FROM [Employees] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Credential?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Credentials.FromSqlInterpolated(
            $"SELECT * FROM [Credentials] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(Credential credential) => context.Credentials.Add(credential);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                error.Number is 2601 or 2627
                && error.Message.Contains("IX_Credentials_Code", StringComparison.Ordinal)))
        {
            throw new DuplicateCredentialCodeException(exception);
        }
    }
}
