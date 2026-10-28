using SGCAE.Domain.Common;

namespace SGCAE.Domain.ValueObjects;

public sealed record CredentialCode
{
    public string Value { get; }

    public CredentialCode(string value)
    {
        Value = Guard.RequiredText(value, "El código de credencial", 100);
    }

    public override string ToString() => Value;
}
