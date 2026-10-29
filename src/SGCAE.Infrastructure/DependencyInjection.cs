using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGCAE.Application.Employees;
using SGCAE.Application.AccessPoints;
using SGCAE.Application.Credentials;
using SGCAE.Application.AccessRecords;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:Sgcae antes de iniciar la aplicación.");

        services.AddDbContext<SgcaeDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeQueries, EmployeeQueries>();
        services.AddScoped<IAccessPointRepository, AccessPointRepository>();
        services.AddScoped<IAccessPointQueries, AccessPointQueries>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddScoped<ICredentialQueries, CredentialQueries>();
        services.AddScoped<IAccessRecordRepository, AccessRecordRepository>();
        return services;
    }
}
