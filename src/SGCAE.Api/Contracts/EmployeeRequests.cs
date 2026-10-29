using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGCAE.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEmployeeRequest(
    [Required] string Identification,
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Email,
    [Required] string JobTitle,
    [Required] string OrganizationalArea);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeRequest(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Email,
    [Required] string JobTitle,
    [Required] string OrganizationalArea);
