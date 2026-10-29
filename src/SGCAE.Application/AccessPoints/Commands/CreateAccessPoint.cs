using SGCAE.Domain.Entities;

namespace SGCAE.Application.AccessPoints.Commands;

public sealed record CreateAccessPointCommand(string Name, bool IsEnabled = true);

public sealed class CreateAccessPointHandler(IAccessPointRepository repository)
{
    public async Task<AccessPointDto> HandleAsync(CreateAccessPointCommand command, CancellationToken cancellationToken = default)
    {
        var point = new AccessPoint(command.Name, command.IsEnabled);
        repository.Add(point);
        await repository.SaveChangesAsync(cancellationToken);
        return new AccessPointDto(point.Id, point.Name, point.IsEnabled);
    }
}
