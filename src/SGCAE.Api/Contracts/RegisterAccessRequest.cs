using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SGCAE.Domain.Enums;

namespace SGCAE.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterAccessRequest([Required] string CredentialCode, Guid AccessPointId, MovementType Movement);
