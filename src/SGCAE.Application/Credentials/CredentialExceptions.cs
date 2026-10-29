namespace SGCAE.Application.Credentials;

public sealed class CredentialNotFoundException(Guid id) : Exception($"No se encontró la credencial {id}.");
public sealed class DuplicateCredentialCodeException(Exception innerException)
    : Exception("El código de credencial ya está registrado.", innerException);
public sealed class CredentialQueryValidationException(string message) : Exception(message);
