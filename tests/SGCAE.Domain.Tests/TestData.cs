using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Tests;

internal static class TestData
{
    public static Employee Employee() => new(new EmployeeIdentification("00123"),
        "Ana", "Pérez", new EmailAddress("ana@example.com"), "Operadora", "Bodega");

    public static DateTimeOffset Timestamp => new(2026, 10, 28, 14, 30, 0, TimeSpan.FromHours(-5));
}
