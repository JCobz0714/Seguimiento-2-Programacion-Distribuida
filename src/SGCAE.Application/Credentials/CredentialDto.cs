using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;

namespace SGCAE.Application.Credentials;

public sealed record CredentialDto(Guid Id, Guid EmployeeId, string Code, CredentialStatus Status)
{
    public static CredentialDto From(Credential credential) =>
        new(credential.Id, credential.EmployeeId, credential.Code.Value, credential.Status);
}

public sealed record CredentialPage(IReadOnlyList<CredentialDto> Items, int Page, int PageSize, int TotalCount);
