using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Entities;

// Evidencia inmutable de un intento, creada exclusivamente por la política de acceso.
public sealed class AccessRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public CredentialCode RequestedCredentialCode { get; private set; } = null!;
    public Guid RequestedAccessPointId { get; private set; }
    public Guid? CredentialId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? AccessPointId { get; private set; }
    public MovementType Movement { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public AccessResult Result { get; private set; }
    public AccessRejectionReason? RejectionReason { get; private set; }

    // Permite rehidratar la entidad sin exponer una construccion invalida al consumidor.
    private AccessRecord() { }

    internal AccessRecord(CredentialCode requestedCredentialCode, Guid requestedAccessPointId,
        Guid? credentialId, Guid? employeeId, Guid? accessPointId, MovementType movement,
        DateTimeOffset occurredAt, AccessRejectionReason? rejectionReason)
    {
        RequestedCredentialCode = requestedCredentialCode;
        RequestedAccessPointId = requestedAccessPointId;
        CredentialId = credentialId;
        EmployeeId = employeeId;
        AccessPointId = accessPointId;
        Movement = movement;
        OccurredAt = occurredAt.ToUniversalTime();
        RejectionReason = rejectionReason;
        Result = rejectionReason.HasValue ? AccessResult.Rejected : AccessResult.Authorized;
    }
}
