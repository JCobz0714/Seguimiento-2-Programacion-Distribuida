using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SGCAE.Application.Employees;
using SGCAE.Application.AccessPoints;
using SGCAE.Domain.Common;

namespace SGCAE.Api.Errors;

internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException or EmployeeQueryValidationException or AccessPointQueryValidationException => (400, "Datos inválidos"),
            AccessPointNotFoundException => (404, "Punto de acceso no encontrado"),
            EmployeeNotFoundException => (404, "Empleado no encontrado"),
            DuplicateEmployeeIdentificationException => (409, "Identificación duplicada"),
            _ => (500, "Error interno")
        };
        if (status == 500)
            logger.LogError(exception, "Error no controlado al procesar la solicitud.");

        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == 500 ? "No fue posible completar la operación." : exception.Message,
                Instance = context.Request.Path
            }
        });
        return true;
    }
}
