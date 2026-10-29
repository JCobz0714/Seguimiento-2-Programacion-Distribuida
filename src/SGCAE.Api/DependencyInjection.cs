using SGCAE.Application.Employees.Commands;
using SGCAE.Application.Employees.Queries;
using SGCAE.Application.AccessPoints.Commands;
using SGCAE.Application.AccessPoints.Queries;
using SGCAE.Application.Credentials.Commands;
using SGCAE.Application.Credentials.Queries;

namespace SGCAE.Api;

internal static class DependencyInjection
{
    public static IServiceCollection AddCredentialUseCases(this IServiceCollection services)
    {
        services.AddScoped<AssignCredentialHandler>();
        services.AddScoped<BlockCredentialHandler>();
        services.AddScoped<RevokeCredentialHandler>();
        services.AddScoped<GetCredentialHandler>();
        services.AddScoped<ListCredentialsHandler>();
        return services;
    }

    public static IServiceCollection AddAccessPointUseCases(this IServiceCollection services)
    {
        services.AddScoped<CreateAccessPointHandler>();
        services.AddScoped<GetAccessPointHandler>();
        services.AddScoped<ListAccessPointsHandler>();
        return services;
    }

    public static IServiceCollection AddEmployeeUseCases(this IServiceCollection services)
    {
        services.AddScoped<CreateEmployeeHandler>();
        services.AddScoped<UpdateEmployeeHandler>();
        services.AddScoped<DeactivateEmployeeHandler>();
        services.AddScoped<GetEmployeeHandler>();
        services.AddScoped<ListEmployeesHandler>();
        return services;
    }
}
