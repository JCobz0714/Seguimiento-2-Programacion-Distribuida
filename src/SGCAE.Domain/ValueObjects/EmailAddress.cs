using System.Net.Mail;
using SGCAE.Domain.Common;

namespace SGCAE.Domain.ValueObjects;

public sealed record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        var normalized = Guard.RequiredText(value, "El correo electrónico", 254);
        if (!MailAddress.TryCreate(normalized, out var address)
            || address.Address != normalized
            || !string.IsNullOrEmpty(address.DisplayName))
            throw new DomainException("El correo electrónico no tiene un formato válido.");

        Value = normalized;
    }

    public override string ToString() => Value;
}
