namespace SGCAE.Domain.Common;

internal static class Guard
{
    public static string RequiredText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} es obligatorio.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} no puede superar {maxLength} caracteres.");

        return trimmed;
    }

    public static Guid NonEmptyId(Guid value, string field)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{field} no puede estar vacío.");
        return value;
    }
}
