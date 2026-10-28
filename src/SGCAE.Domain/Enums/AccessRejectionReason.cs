namespace SGCAE.Domain.Enums;

public enum AccessRejectionReason
{
    UnknownCredential = 1,
    EmployeeNotFound = 2,
    InactiveEmployee = 3,
    BlockedCredential = 4,
    RevokedCredential = 5,
    UnknownAccessPoint = 6,
    DisabledAccessPoint = 7
}
