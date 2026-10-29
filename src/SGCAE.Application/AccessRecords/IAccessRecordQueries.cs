using SGCAE.Application.AccessRecords.Queries;

namespace SGCAE.Application.AccessRecords;

public interface IAccessRecordQueries
{
    Task<AccessRecordPage> ListAsync(ListAccessRecordsQuery query, CancellationToken cancellationToken);
}
