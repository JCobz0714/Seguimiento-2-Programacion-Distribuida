using SGCAE.Domain.Common;

namespace SGCAE.Domain.Entities;

public sealed class AccessPoint
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public bool IsEnabled { get; private set; }

    public AccessPoint(string name, bool isEnabled = true)
    {
        Name = Guard.RequiredText(name, "El nombre del punto de acceso", 100);
        IsEnabled = isEnabled;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
