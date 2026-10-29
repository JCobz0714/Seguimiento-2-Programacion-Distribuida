namespace SGCAE.Application.Employees;

public sealed class EmployeeNotFoundException(Guid id)
    : Exception($"No se encontró el empleado {id}.");

public sealed class DuplicateEmployeeIdentificationException(Exception innerException)
    : Exception("La identificación ya está registrada.", innerException);

public sealed class EmployeeQueryValidationException(string message) : Exception(message);
