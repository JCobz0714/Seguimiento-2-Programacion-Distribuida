using Microsoft.EntityFrameworkCore;
using SGCAE.Domain.Entities;

namespace SGCAE.Infrastructure.Persistence;

public sealed class SgcaeDbContext(DbContextOptions<SgcaeDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<AccessPoint> AccessPoints => Set<AccessPoint>();
    public DbSet<AccessRecord> AccessRecords => Set<AccessRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SgcaeDbContext).Assembly);
    }
}
