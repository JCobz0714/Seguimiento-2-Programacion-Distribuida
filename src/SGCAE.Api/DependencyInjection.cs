using SGCAE.Application.Employees.Commands;
using SGCAE.Application.Employees.Queries;
using SGCAE.Application.AccessPoints.Commands;
using SGCAE.Application.AccessPoints.Queries;

namespace SGCAE.Api;

internal static class DependencyInjection
{
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
