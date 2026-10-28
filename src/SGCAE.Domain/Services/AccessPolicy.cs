using SGCAE.Domain.Common;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Services;

public sealed class AccessPolicy
{
    public AccessRecord Evaluate(CredentialCode requestedCode, Guid requestedPointId,
        MovementType movement, DateTimeOffset occurredAt, Credential? credential,
        Employee? employee, AccessPoint? accessPoint)
    {
        ArgumentNullException.ThrowIfNull(requestedCode);
        Guard.NonEmptyId(requestedPointId, "El punto de acceso");
        if (!Enum.IsDefined(movement))
            throw new DomainException("El movimiento debe ser entrada o salida.");
        if (occurredAt == default)
            throw new DomainException("La fecha del movimiento es obligatoria.");

        // Los objetos resueltos por la aplicación deben corresponder a la solicitud.
        if (credential is not null && credential.Code != requestedCode)
            throw new DomainException("La credencial no corresponde al código solicitado.");
        if (employee is not null && (credential is null || credential.EmployeeId != employee.Id))
            throw new DomainException("El empleado no corresponde a la credencial.");
        if (accessPoint is not null && accessPoint.Id != requestedPointId)
            throw new DomainException("El punto de acceso no corresponde a la solicitud.");

        var reason = GetRejectionReason(credential, employee, accessPoint);
        return new AccessRecord(requestedCode, requestedPointId, credential?.Id, employee?.Id,
            accessPoint?.Id, movement, occurredAt, reason);
    }

    private static AccessRejectionReason? GetRejectionReason(
        Credential? credential, Employee? employee, AccessPoint? accessPoint)
    {
        if (credential is null) return AccessRejectionReason.UnknownCredential;
        if (employee is null) return AccessRejectionReason.EmployeeNotFound;
        if (employee.Status != EmploymentStatus.Active) return AccessRejectionReason.InactiveEmployee;
        if (credential.Status == CredentialStatus.Blocked) return AccessRejectionReason.BlockedCredential;
        if (credential.Status == CredentialStatus.Revoked) return AccessRejectionReason.RevokedCredential;
        if (accessPoint is null) return AccessRejectionReason.UnknownAccessPoint;
        if (!accessPoint.IsEnabled) return AccessRejectionReason.DisabledAccessPoint;
        return null;
    }
}
