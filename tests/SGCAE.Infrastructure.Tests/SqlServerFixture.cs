using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Infrastructure.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string databaseName = $"SGCAE_Tests_{Guid.NewGuid():N}";
    private bool ownsDatabase;
    private string connectionString = null!;

    public SgcaeDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgcaeDbContext>().UseSqlServer(connectionString).Options);

    public async Task InitializeAsync()
    {
        var builder = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("SGCAE_TEST_SQLSERVER")
            ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
        {
            InitialCatalog = "master",
            AttachDBFilename = "",
            Pooling = false
        };

        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            // Nombre generado exclusivamente aquí; CREATE falla si ya existe.
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
            ownsDatabase = true;
        }

        builder.InitialCatalog = databaseName;
        connectionString = builder.ConnectionString;
        try
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!ownsDatabase) return;
        // Solo se elimina la base aleatoria creada por esta instancia del fixture.
        if (!databaseName.StartsWith("SGCAE_Tests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Nombre de base de pruebas inesperado.");

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        ownsDatabase = false;
    }
}
