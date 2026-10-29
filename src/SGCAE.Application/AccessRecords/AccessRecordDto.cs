using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;

namespace SGCAE.Application.AccessRecords;

public sealed record AccessRecordDto(Guid Id, string RequestedCredentialCode, Guid RequestedAccessPointId,
    Guid? CredentialId, Guid? EmployeeId, Guid? AccessPointId, MovementType Movement,
    DateTimeOffset OccurredAt, AccessResult Result, AccessRejectionReason? RejectionReason)
{
    public static AccessRecordDto From(AccessRecord record) => new(record.Id, record.RequestedCredentialCode.Value,
        record.RequestedAccessPointId, record.CredentialId, record.EmployeeId, record.AccessPointId,
        record.Movement, record.OccurredAt, record.Result, record.RejectionReason);
}
