namespace SGCAE.Application.AccessPoints;

public sealed class AccessPointNotFoundException(Guid id)
    : Exception($"No se encontró el punto de acceso {id}.");

public sealed class AccessPointQueryValidationException(string message) : Exception(message);
