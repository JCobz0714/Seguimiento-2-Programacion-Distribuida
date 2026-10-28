using SGCAE.Domain.Common;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Tests;

public class EntityTests
{
    [Fact]
    public void InvalidEmployeeUpdateDoesNotPartiallyChangeExistingDetails()
    {
        var employee = TestData.Employee();
        Assert.Throws<DomainException>(() => employee.UpdateDetails("Nuevo", "Apellido",
            new EmailAddress("nuevo@example.com"), "Supervisor", " "));
        Assert.Equal("Ana", employee.FirstName);
        Assert.Equal("Pérez", employee.LastName);
        Assert.Equal("ana@example.com", employee.Email.Value);
        Assert.Equal("Operadora", employee.JobTitle);
        Assert.Equal("Bodega", employee.OrganizationalArea);
    }

    [Fact]
    public void UpdatingDetailsPreservesIdentityAndInactiveStatus()
    {
        var employee = TestData.Employee();
        var id = employee.Id;
        employee.Deactivate();
        employee.Deactivate();
        employee.UpdateDetails(" Ana María ", " Gómez ", new EmailAddress("ana2@example.com"),
            " Supervisora ", " Logística ");
        Assert.Equal(id, employee.Id);
        Assert.Equal("00123", employee.Identification.Value);
        Assert.Equal(EmploymentStatus.Inactive, employee.Status);
        Assert.Equal("Ana María", employee.FirstName);
        Assert.Equal("Gómez", employee.LastName);
        Assert.Equal("ana2@example.com", employee.Email.Value);
        Assert.Equal("Supervisora", employee.JobTitle);
        Assert.Equal("Logística", employee.OrganizationalArea);
    }

    [Theory]
    [InlineData("", "Pérez", "Operadora", "Bodega")]
    [InlineData("Ana", "", "Operadora", "Bodega")]
    [InlineData("Ana", "Pérez", "", "Bodega")]
    [InlineData("Ana", "Pérez", "Operadora", "")]
    public void EmployeeRequiresAllBusinessDetails(string first, string last, string job, string area)
    {
        Assert.Throws<DomainException>(() => new Employee(new EmployeeIdentification("123"),
            first, last, new EmailAddress("ana@example.com"), job, area));
    }

    [Fact]
    public void InactiveEmployeeCannotReceiveCredential()
    {
        var employee = TestData.Employee();
        employee.Deactivate();
        Assert.Throws<DomainException>(() => new Credential(employee, new CredentialCode("C1")));
    }

    [Fact]
    public void EmployeeMayHaveMultipleIndependentCredentials()
    {
        var employee = TestData.Employee();
        var first = new Credential(employee, new CredentialCode("C1"));
        var second = new Credential(employee, new CredentialCode("C2"));
        first.Block();
        first.Block();
        Assert.Equal(CredentialStatus.Blocked, first.Status);
        Assert.Equal(CredentialStatus.Active, second.Status);
        Assert.Equal(employee.Id, second.EmployeeId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevocationIsFinalFromActiveOrBlockedState(bool blocked)
    {
        var credential = new Credential(TestData.Employee(), new CredentialCode("C1"));
        if (blocked) credential.Block();
        credential.Revoke();
        credential.Revoke();
        Assert.Throws<DomainException>(() => credential.Block());
        Assert.Equal(CredentialStatus.Revoked, credential.Status);
    }

    [Fact]
    public void AccessPointRequiresNameAndCanChangeAvailability()
    {
        Assert.Throws<DomainException>(() => new AccessPoint(" "));
        Assert.Throws<DomainException>(() => new AccessPoint(new string('a', 101)));
        var point = new AccessPoint(" Bodega ", false);
        Assert.Equal("Bodega", point.Name);
        Assert.False(point.IsEnabled);
        point.Enable();
        Assert.True(point.IsEnabled);
        point.Disable();
        Assert.False(point.IsEnabled);
    }
}
