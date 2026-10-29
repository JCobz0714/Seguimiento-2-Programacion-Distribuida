using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGCAE.Application.AccessRecords;
using SGCAE.Application.AccessRecords.Queries;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.Services;
using SGCAE.Domain.ValueObjects;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Api.Tests;

public sealed class AccessHistoryEndpointsTests(SgcaeApiFactory factory) : IClassFixture<SgcaeApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/access-records";
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private HttpClient client = null!;
    private Guid employeeId, pointId, unknownPointId;
    private AccessRecord[] records = [];

    private static Employee Employee(string identification) => new(new EmployeeIdentification(identification),
        "Ana", "Pérez", new EmailAddress("ana@example.com"), "Operadora", "Bodega");

    public async Task InitializeAsync()
    {
        await using var context = factory.Database.CreateContext();
        await context.AccessRecords.ExecuteDeleteAsync();
        await context.Credentials.ExecuteDeleteAsync();
        await context.AccessPoints.ExecuteDeleteAsync();
        await context.Employees.ExecuteDeleteAsync();
        var employee = Employee("001");
        var other = Employee("002");
        var credential = new Credential(employee, new CredentialCode("C-001"));
        var second = new Credential(other, new CredentialCode("C-002"));
        var point = new AccessPoint("Principal");
        var anotherPoint = new AccessPoint("Secundario");
        employeeId = employee.Id;
        pointId = point.Id;
        unknownPointId = Guid.NewGuid();
        var policy = new AccessPolicy();
        AccessRecord Record(Credential? c, Employee? e, AccessPoint? p, int minute, MovementType movement = MovementType.Entry) =>
            policy.Evaluate(c?.Code ?? new CredentialCode("UNKNOWN"), p?.Id ?? unknownPointId,
                movement, Start.AddMinutes(minute), c, e, p);
        var initial = new[]
        {
            Record(credential, employee, point, 0),
            Record(credential, employee, anotherPoint, 1, MovementType.Exit),
            Record(second, other, point, 2)
        };
        credential.Block();
        records = [..initial, Record(credential, employee, point, 2),
            Record(null, null, point, 3), Record(second, other, null, 4),
            Record(null, null, null, 4), Record(second, other, anotherPoint, 5, MovementType.Exit)];
        context.AddRange(employee, other, credential, second, point, anotherPoint);
        context.AccessRecords.AddRange(records);
        await context.SaveChangesAsync();
        client = factory.CreateClient();
    }

    public Task DisposeAsync() { client.Dispose(); return Task.CompletedTask; }

    private async Task<AccessRecordPage> GetAsync(string query = "")
    {
        using var response = await client.GetAsync(Route + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccessRecordPage>(JsonOptions))!;
    }

    public static IEnumerable<object[]> FilterCombinations() =>
        Enumerable.Range(0, 32).Select(mask => new object[] { mask });

    [Theory]
    [MemberData(nameof(FilterCombinations))]
    public async Task AllFilterCombinationsApplyAsIntersectionWithFilteredTotal(int mask)
    {
        var filters = new List<string> { "pageSize=100" };
        IEnumerable<AccessRecord> expected = records;
        if ((mask & 1) != 0)
        {
            filters.Add($"employeeId={employeeId}");
            expected = expected.Where(r => r.EmployeeId == employeeId);
        }
        if ((mask & 2) != 0)
        {
            filters.Add($"accessPointId={pointId}");
            expected = expected.Where(r => r.RequestedAccessPointId == pointId);
        }
        if ((mask & 4) != 0)
        {
            filters.Add("result=Rejected");
            expected = expected.Where(r => r.Result == AccessResult.Rejected);
        }
        if ((mask & 8) != 0)
        {
            filters.Add($"from={Uri.EscapeDataString(Start.AddMinutes(2).ToString("O"))}");
            expected = expected.Where(r => r.OccurredAt >= Start.AddMinutes(2));
        }
        if ((mask & 16) != 0)
        {
            filters.Add($"to={Uri.EscapeDataString(Start.AddMinutes(4).ToString("O"))}");
            expected = expected.Where(r => r.OccurredAt < Start.AddMinutes(4));
        }
        var result = await GetAsync("?" + string.Join("&", filters));
        var expectedRecords = expected.ToDictionary(r => r.Id);
        Assert.Equal(expectedRecords.Count, result.TotalCount);
        Assert.Equal(expectedRecords.Count, result.Items.Count);
        Assert.Equal(expectedRecords.Keys.Order(), result.Items.Select(r => r.Id).Order());
        foreach (var item in result.Items) Assert.Equal(AccessRecordDto.From(expectedRecords[item.Id]), item);
    }

    [Fact]
    public async Task PaginationIsStableAcrossEqualTimestampsWithoutOverlaps()
    {
        Guid[] expected;
        await using (var context = factory.Database.CreateContext())
            expected = await context.AccessRecords.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Id)
                .Select(r => r.Id).ToArrayAsync();
        var ids = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await GetAsync($"?page={page}&pageSize=2");
            Assert.Equal(page, result.Page);
            Assert.Equal(2, result.PageSize);
            Assert.Equal(8, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
            ids.AddRange(result.Items.Select(r => r.Id));
            var repeat = await GetAsync($"?page={page}&pageSize=2");
            Assert.Equal(result.Items.Select(r => r.Id), repeat.Items.Select(r => r.Id));
        }
        Assert.Equal(expected, ids);
        Assert.Equal(8, ids.Distinct().Count());
        var beyond = await GetAsync("?page=5&pageSize=2");
        Assert.Empty(beyond.Items);
        Assert.Equal(8, beyond.TotalCount);
    }

    [Fact]
    public async Task FilteringHappensBeforePagination()
    {
        var all = await GetAsync($"?employeeId={employeeId}&result=Rejected");
        var first = await GetAsync($"?employeeId={employeeId}&result=Rejected&pageSize=1");
        var second = await GetAsync($"?employeeId={employeeId}&result=Rejected&pageSize=1&page=2");
        Assert.Equal(1, first.TotalCount);
        Assert.Equal(Assert.Single(all.Items).Id, Assert.Single(first.Items).Id);
        Assert.Empty(second.Items);
        Assert.Equal(1, second.TotalCount);
    }

    [Fact]
    public async Task DateBoundsAreInclusiveFromExclusiveToAndOffsetsRepresentSameInstant()
    {
        var from = Uri.EscapeDataString(Start.AddMinutes(2).ToOffset(TimeSpan.FromHours(-5)).ToString("O"));
        var to = Uri.EscapeDataString(Start.AddMinutes(4).ToOffset(TimeSpan.FromHours(2)).ToString("O"));
        var result = await GetAsync($"?from={from}&to={to}");
        Assert.Equal(3, result.TotalCount);
        Assert.Contains(result.Items, r => r.OccurredAt == Start.AddMinutes(2));
        Assert.All(result.Items, r => Assert.True(r.OccurredAt >= Start.AddMinutes(2) && r.OccurredAt < Start.AddMinutes(4)));
    }

    [Fact]
    public async Task UnknownRequestedPointCanBeFilteredAndUnknownReferencesRemainNull()
    {
        var result = await GetAsync($"?accessPointId={unknownPointId}&result=Rejected");
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r =>
        {
            Assert.Equal(unknownPointId, r.RequestedAccessPointId);
            Assert.Null(r.AccessPointId);
        });
        var unknownCredential = Assert.Single(result.Items, r => r.RejectionReason == AccessRejectionReason.UnknownCredential);
        Assert.Null(unknownCredential.EmployeeId);
        Assert.Null(unknownCredential.CredentialId);
        Assert.Equal("UNKNOWN", unknownCredential.RequestedCredentialCode);
    }

    [Fact]
    public async Task DefaultsAndAuthorizedFilterReturnExpectedRecords()
    {
        var all = await GetAsync();
        Assert.Equal(1, all.Page);
        Assert.Equal(20, all.PageSize);
        Assert.Equal(8, all.TotalCount);
        Assert.Equal(all.Items.Select(r => r.OccurredAt).OrderDescending(), all.Items.Select(r => r.OccurredAt));
        var authorized = await GetAsync("?result=Authorized");
        Assert.Equal(4, authorized.TotalCount);
        Assert.All(authorized.Items, r => Assert.Equal(AccessResult.Authorized, r.Result));
    }

    [Fact]
    public async Task EmptyDatabaseAndUnmatchedFiltersReturnEmptyPages()
    {
        Assert.Empty((await GetAsync($"?employeeId={Guid.NewGuid()}")).Items);
        Assert.Empty((await GetAsync($"?accessPointId={Guid.NewGuid()}")).Items);
        await using var context = factory.Database.CreateContext();
        await context.AccessRecords.ExecuteDeleteAsync();
        var result = await GetAsync();
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=invalid")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("employeeId=bad")]
    [InlineData("employeeId=00000000-0000-0000-0000-000000000000")]
    [InlineData("accessPointId=00000000-0000-0000-0000-000000000000")]
    [InlineData("result=999")]
    [InlineData("result=Unknown")]
    [InlineData("from=not-a-date")]
    [InlineData("to=not-a-date")]
    [InlineData("from=2026-10-29T12:00:00Z&to=2026-10-29T12:00:00Z")]
    [InlineData("from=2026-10-30T12:00:00Z&to=2026-10-29T12:00:00Z")]
    public async Task InvalidQueryReturnsProblemDetailsAndDoesNotChangeHistory(string query)
    {
        using var response = await client.GetAsync(Route + "?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(8, await context.AccessRecords.CountAsync());
    }

    [Fact]
    public async Task HistoricalResultsDoNotChangeWhenEmployeeIsDeactivated()
    {
        var before = await GetAsync($"?employeeId={employeeId}");
        using var response = await client.PatchAsync($"/api/employees/{employeeId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await GetAsync($"?employeeId={employeeId}");
        Assert.Equal(before.Items.ToArray(), after.Items.ToArray());
    }

    [Fact]
    public async Task QueriesDoNotTrackRecords()
    {
        using var scope = factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAccessRecordQueries>();
        var result = await queries.ListAsync(new ListAccessRecordsQuery(), default);
        Assert.Equal(8, result.TotalCount);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SgcaeDbContext>().ChangeTracker.Entries());
    }
}
