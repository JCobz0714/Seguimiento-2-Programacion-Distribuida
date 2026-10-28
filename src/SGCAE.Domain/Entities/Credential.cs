using SGCAE.Domain.Common;
using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Entities;

public sealed class Credential
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid EmployeeId { get; }
    public CredentialCode Code { get; }
    public CredentialStatus Status { get; private set; } = CredentialStatus.Active;

    public Credential(Employee employee, CredentialCode code)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(code);
        if (employee.Status != EmploymentStatus.Active)
            throw new DomainException("No se puede asignar una credencial a un empleado inactivo.");

        EmployeeId = employee.Id;
        Code = code;
    }

    public void Block()
    {
        if (Status == CredentialStatus.Revoked)
            throw new DomainException("Una credencial revocada no puede bloquearse.");

        Status = CredentialStatus.Blocked;
    }

    public void Revoke() => Status = CredentialStatus.Revoked;
}
