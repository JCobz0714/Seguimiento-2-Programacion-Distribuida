using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGCAE.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignCredentialRequest(Guid EmployeeId, [Required] string Code);
