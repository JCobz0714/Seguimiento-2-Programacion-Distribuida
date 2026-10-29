using SGCAE.Domain.Common;
using SGCAE.Domain.Enums;
using SGCAE.Domain.Services;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Application.AccessRecords.Commands;

public sealed record RegisterAccessCommand(string CredentialCode, Guid AccessPointId, MovementType Movement);

public sealed class RegisterAccessHandler(IAccessRecordRepository repository, AccessPolicy policy, TimeProvider clock)
{
    public Task<AccessRecordDto> HandleAsync(RegisterAccessCommand command, CancellationToken cancellationToken = default)
    {
        var code = new CredentialCode(command.CredentialCode);
        if (command.AccessPointId == Guid.Empty)
            throw new DomainException("El punto de acceso es obligatorio.");
        if (!Enum.IsDefined(command.Movement))
            throw new DomainException("El movimiento debe ser Entry o Exit.");

        return repository.ExecuteAsync(async token =>
        {
            var credential = await repository.GetCredentialAsync(code, token);
            var employee = credential is null ? null : await repository.GetEmployeeAsync(credential.EmployeeId, token);
            var point = await repository.GetAccessPointAsync(command.AccessPointId, token);
            var record = policy.Evaluate(code, command.AccessPointId, command.Movement,
                clock.GetUtcNow(), credential, employee, point);
            repository.Add(record);
            await repository.SaveChangesAsync(token);
            return AccessRecordDto.From(record);
        }, cancellationToken);
    }
}
