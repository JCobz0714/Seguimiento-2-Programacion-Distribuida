using SGCAE.Domain.Common;

namespace SGCAE.Domain.ValueObjects;

public sealed record EmployeeIdentification
{
    public string Value { get; }

    public EmployeeIdentification(string value)
    {
        Value = Guard.RequiredText(value, "La identificación", 30);
    }

    public override string ToString() => Value;
}
