using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Entities;

// Evidencia inmutable de un intento, creada exclusivamente por la política de acceso.
public sealed class AccessRecord
{
    public Guid Id { get; } = Guid.NewGuid();
    public CredentialCode RequestedCredentialCode { get; }
    public Guid RequestedAccessPointId { get; }
    public Guid? CredentialId { get; }
    public Guid? EmployeeId { get; }
    public Guid? AccessPointId { get; }
    public MovementType Movement { get; }
    public DateTimeOffset OccurredAt { get; }
    public AccessResult Result { get; }
    public AccessRejectionReason? RejectionReason { get; }

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
