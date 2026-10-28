using SGCAE.Domain.Common;

namespace SGCAE.Domain.Entities;

public sealed class AccessPoint
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = null!;
    public bool IsEnabled { get; private set; }

    // Permite rehidratar la entidad sin exponer una construccion invalida al consumidor.
    private AccessPoint() { }

    public AccessPoint(string name, bool isEnabled = true)
    {
        Name = Guard.RequiredText(name, "El nombre del punto de acceso", 100);
        IsEnabled = isEnabled;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
