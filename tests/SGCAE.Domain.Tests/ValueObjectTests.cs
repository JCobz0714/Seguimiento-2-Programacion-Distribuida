using SGCAE.Domain.Common;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Tests;

public class ValueObjectTests
{
    [Fact]
    public void IdentificationPreservesLeadingZerosAndComparesByValue()
    {
        var value = new EmployeeIdentification(" 00123 ");
        Assert.Equal(new EmployeeIdentification("00123"), value);
        Assert.NotEqual(new EmployeeIdentification("123"), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequiredValuesRejectMissingText(string? value)
    {
        Assert.Throws<DomainException>(() => new EmployeeIdentification(value!));
        Assert.Throws<DomainException>(() => new CredentialCode(value!));
        Assert.Throws<DomainException>(() => new EmailAddress(value!));
    }

    [Fact]
    public void ValuesEnforceDocumentedLengthBoundaries()
    {
        Assert.Equal(30, new EmployeeIdentification(new string('1', 30)).Value.Length);
        Assert.Throws<DomainException>(() => new EmployeeIdentification(new string('1', 31)));
        Assert.Equal(100, new CredentialCode(new string('A', 100)).Value.Length);
        Assert.Throws<DomainException>(() => new CredentialCode(new string('A', 101)));
        Assert.Throws<DomainException>(() => new EmailAddress(new string('a', 245) + "@example.com"));
    }

    [Theory]
    [InlineData("no-es-correo")]
    [InlineData("a@@example.com")]
    [InlineData("Ana <ana@example.com>")]
    [InlineData("ana@example.com;otra@example.com")]
    public void EmailRejectsInvalidOrDecoratedAddresses(string value)
    {
        Assert.Throws<DomainException>(() => new EmailAddress(value));
    }

    [Fact]
    public void CodeIsTrimmedButCaseSensitive()
    {
        Assert.Equal(new CredentialCode("AbC"), new CredentialCode(" AbC "));
        Assert.NotEqual(new CredentialCode("AbC"), new CredentialCode("abc"));
        Assert.Equal(new EmailAddress("ana@example.com"), new EmailAddress(" ana@example.com "));
    }
}
