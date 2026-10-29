namespace SGCAE.Application.AccessRecords;

public sealed record AccessRecordPage(IReadOnlyList<AccessRecordDto> Items, int Page, int PageSize, int TotalCount);
