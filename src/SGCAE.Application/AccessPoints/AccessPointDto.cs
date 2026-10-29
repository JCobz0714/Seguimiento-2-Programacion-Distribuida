namespace SGCAE.Application.AccessPoints;

public sealed record AccessPointDto(Guid Id, string Name, bool IsEnabled);

public sealed record AccessPointPage(IReadOnlyList<AccessPointDto> Items, int Page, int PageSize, int TotalCount);
