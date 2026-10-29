using SGCAE.Application.Employees.Commands;
using SGCAE.Application.Employees.Queries;

namespace SGCAE.Api;

internal static class DependencyInjection
{
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
