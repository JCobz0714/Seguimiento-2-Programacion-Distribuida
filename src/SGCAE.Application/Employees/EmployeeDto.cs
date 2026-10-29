using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;

namespace SGCAE.Application.Employees;

public sealed record EmployeeDto(Guid Id, string Identification, string FirstName,
    string LastName, string Email, string JobTitle, string OrganizationalArea, EmploymentStatus Status)
{
    public static EmployeeDto From(Employee employee) => new(employee.Id,
        employee.Identification.Value, employee.FirstName, employee.LastName,
        employee.Email.Value, employee.JobTitle, employee.OrganizationalArea, employee.Status);
}

public sealed record EmployeePage(IReadOnlyList<EmployeeDto> Items, int Page, int PageSize, int TotalCount);
