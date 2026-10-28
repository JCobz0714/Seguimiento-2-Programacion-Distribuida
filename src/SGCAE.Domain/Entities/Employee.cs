using SGCAE.Domain.Common;
using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Entities;

public sealed class Employee
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public EmployeeIdentification Identification { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;
    public string JobTitle { get; private set; } = null!;
    public string OrganizationalArea { get; private set; } = null!;
    public EmploymentStatus Status { get; private set; } = EmploymentStatus.Active;

    // Permite rehidratar la entidad sin exponer una construccion invalida al consumidor.
    private Employee() { }

    public Employee(EmployeeIdentification identification, string firstName, string lastName,
        EmailAddress email, string jobTitle, string organizationalArea)
    {
        ArgumentNullException.ThrowIfNull(identification);
        Identification = identification;
        UpdateDetails(firstName, lastName, email, jobTitle, organizationalArea);
    }

    public void UpdateDetails(string firstName, string lastName, EmailAddress email,
        string jobTitle, string organizationalArea)
    {
        ArgumentNullException.ThrowIfNull(email);
        // Validar todo antes de cambiar el estado evita actualizaciones parciales.
        var validFirstName = Guard.RequiredText(firstName, "Los nombres", 100);
        var validLastName = Guard.RequiredText(lastName, "Los apellidos", 100);
        var validJobTitle = Guard.RequiredText(jobTitle, "El cargo", 100);
        var validArea = Guard.RequiredText(organizationalArea, "El área organizacional", 100);

        FirstName = validFirstName;
        LastName = validLastName;
        Email = email;
        JobTitle = validJobTitle;
        OrganizationalArea = validArea;
    }

    public void Deactivate() => Status = EmploymentStatus.Inactive;
}
