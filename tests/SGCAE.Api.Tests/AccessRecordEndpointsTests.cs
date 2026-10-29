using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGCAE.Api.Contracts;
using SGCAE.Application.AccessRecords;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.ValueObjects;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Api.Tests;

public sealed class AccessRecordEndpointsTests(SgcaeApiFactory factory) : IClassFixture<SgcaeApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/access-records";
    private HttpClient client = null!;
    private Guid employeeId, credentialId, pointId;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await using var context = factory.Database.CreateContext();
        await context.AccessRecords.ExecuteDeleteAsync();
        await context.Credentials.ExecuteDeleteAsync();
        await context.AccessPoints.ExecuteDeleteAsync();
        await context.Employees.ExecuteDeleteAsync();
        var employee = new Employee(new EmployeeIdentification("001"), "Ana", "Pérez",
            new EmailAddress("ana@example.com"), "Operadora", "Bodega");
        var credential = new Credential(employee, new CredentialCode("C-001"));
        var point = new AccessPoint("Principal");
        context.AddRange(employee, credential, point);
        await context.SaveChangesAsync();
        (employeeId, credentialId, pointId) = (employee.Id, credential.Id, point.Id);
        client = factory.CreateClient();
    }

    public Task DisposeAsync() { client.Dispose(); return Task.CompletedTask; }

    private async Task<AccessRecordDto> RegisterAsync(MovementType movement, string code = "C-001", Guid? point = null)
    {
        using var response = await client.PostAsJsonAsync(Route, new RegisterAccessRequest(code, point ?? pointId, movement), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var record = (await response.Content.ReadFromJsonAsync<AccessRecordDto>(JsonOptions))!;
        await using var context = factory.Database.CreateContext();
        Assert.Equal(record, AccessRecordDto.From(await context.AccessRecords.SingleAsync(r => r.Id == record.Id)));
        return record;
    }

    [Theory]
    [InlineData(MovementType.Entry)]
    [InlineData(MovementType.Exit)]
    public async Task ValidAccessIsAuthorizedAndStoredWithServerUtcDate(MovementType movement)
    {
        var before = DateTimeOffset.UtcNow;
        var record = await RegisterAsync(movement, " C-001 ");
        Assert.Equal(AccessResult.Authorized, record.Result);
        Assert.Null(record.RejectionReason);
        Assert.Equal("C-001", record.RequestedCredentialCode);
        Assert.Equal(pointId, record.RequestedAccessPointId);
        Assert.Equal(pointId, record.AccessPointId);
        Assert.Equal(employeeId, record.EmployeeId);
        Assert.Equal(credentialId, record.CredentialId);
        Assert.Equal(movement, record.Movement);
        Assert.Equal(TimeSpan.Zero, record.OccurredAt.Offset);
        Assert.InRange(record.OccurredAt, before, DateTimeOffset.UtcNow);
    }

    public static IEnumerable<object[]> RejectionCases()
    {
        foreach (var reason in new[] { AccessRejectionReason.UnknownCredential, AccessRejectionReason.InactiveEmployee,
            AccessRejectionReason.BlockedCredential, AccessRejectionReason.RevokedCredential,
            AccessRejectionReason.UnknownAccessPoint, AccessRejectionReason.DisabledAccessPoint })
        foreach (var movement in new[] { MovementType.Entry, MovementType.Exit })
            yield return [reason, movement];
    }

    [Theory]
    [MemberData(nameof(RejectionCases))]
    public async Task RejectionsAreStoredWithReasonAndKnownReferences(AccessRejectionReason reason, MovementType movement)
    {
        var code = reason == AccessRejectionReason.UnknownCredential ? "UNKNOWN" : "C-001";
        var requestedPoint = reason == AccessRejectionReason.UnknownAccessPoint ? Guid.NewGuid() : pointId;
        await using (var context = factory.Database.CreateContext())
        {
            if (reason == AccessRejectionReason.InactiveEmployee) (await context.Employees.SingleAsync()).Deactivate();
            if (reason == AccessRejectionReason.BlockedCredential) (await context.Credentials.SingleAsync()).Block();
            if (reason == AccessRejectionReason.RevokedCredential) (await context.Credentials.SingleAsync()).Revoke();
            if (reason == AccessRejectionReason.DisabledAccessPoint) (await context.AccessPoints.SingleAsync()).Disable();
            await context.SaveChangesAsync();
        }
        var record = await RegisterAsync(movement, code, requestedPoint);
        Assert.Equal(AccessResult.Rejected, record.Result);
        Assert.Equal(reason, record.RejectionReason);
        Assert.Equal(movement, record.Movement);
        Assert.Equal(code, record.RequestedCredentialCode);
        Assert.Equal(requestedPoint, record.RequestedAccessPointId);
        Assert.Equal(reason == AccessRejectionReason.UnknownCredential ? (Guid?)null : credentialId, record.CredentialId);
        Assert.Equal(reason == AccessRejectionReason.UnknownCredential ? (Guid?)null : employeeId, record.EmployeeId);
        Assert.Equal(reason == AccessRejectionReason.UnknownAccessPoint ? (Guid?)null : pointId, record.AccessPointId);
    }

    [Fact]
    public async Task UnknownCodeAndPointAreRetainedWithoutInventingReferences()
    {
        var requested = Guid.NewGuid();
        var record = await RegisterAsync(MovementType.Entry, "UNKNOWN", requested);
        Assert.Equal(AccessRejectionReason.UnknownCredential, record.RejectionReason);
        Assert.Equal(requested, record.RequestedAccessPointId);
        Assert.Null(record.CredentialId);
        Assert.Null(record.EmployeeId);
        Assert.Null(record.AccessPointId);
    }

    [Fact]
    public async Task InactiveEmployeeTakesPriorityOverRevokedCredentialAndDisabledPoint()
    {
        await using (var context = factory.Database.CreateContext())
        {
            (await context.Employees.SingleAsync()).Deactivate();
            (await context.Credentials.SingleAsync()).Revoke();
            (await context.AccessPoints.SingleAsync()).Disable();
            await context.SaveChangesAsync();
        }
        Assert.Equal(AccessRejectionReason.InactiveEmployee, (await RegisterAsync(MovementType.Exit)).RejectionReason);
    }

    [Fact]
    public async Task CodeLookupIsCaseSensitive()
    {
        Assert.Equal(AccessRejectionReason.UnknownCredential,
            (await RegisterAsync(MovementType.Entry, "c-001")).RejectionReason);
    }

    [Fact]
    public async Task RepeatedMovementsProduceIndependentRecords()
    {
        var first = await RegisterAsync(MovementType.Entry);
        var second = await RegisterAsync(MovementType.Entry);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(AccessResult.Authorized, second.Result);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(2, await context.AccessRecords.CountAsync());
    }

    [Fact]
    public async Task RevocationChangesFutureDecisionWithoutChangingEarlierEvidence()
    {
        var first = await RegisterAsync(MovementType.Entry);
        using var revoke = await client.PatchAsync($"/api/credentials/{credentialId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var second = await RegisterAsync(MovementType.Exit);
        Assert.Equal(AccessRejectionReason.RevokedCredential, second.RejectionReason);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(first, AccessRecordDto.From(await context.AccessRecords.SingleAsync(r => r.Id == first.Id)));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData(" ", 1)]
    [InlineData("C-001", 0)]
    [InlineData("C-001", 99)]
    public async Task InvalidValuesReturnBadRequestWithoutRecords(string? code, int movement)
    {
        using var response = await client.PostAsJsonAsync(Route, new { credentialCode = code, accessPointId = pointId, movement });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessRecords.AnyAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"credentialCode\":\"C-001\",\"movement\":\"Entry\"}")]
    [InlineData("{\"credentialCode\":\"C-001\",\"accessPointId\":\"bad\",\"movement\":\"Entry\"}")]
    public async Task MalformedBodyDoesNotPersist(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route, content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessRecords.AnyAsync());
    }

    [Theory]
    [InlineData("occurredAt", "2000-01-01T00:00:00Z")]
    [InlineData("result", "Authorized")]
    [InlineData("employeeId", "00000000-0000-0000-0000-000000000001")]
    public async Task ClientCannotSupplyDecisionOrDate(string field, string value)
    {
        var body = JsonSerializer.SerializeToNode(new RegisterAccessRequest("C-001", pointId, MovementType.Entry), JsonOptions)!;
        body[field] = value;
        using var response = await client.PostAsJsonAsync(Route, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessRecords.AnyAsync());
    }

    [Fact]
    public async Task OversizedCodeDoesNotPersist()
    {
        using var response = await client.PostAsJsonAsync(Route, new RegisterAccessRequest(new string('X', 101), pointId, MovementType.Entry));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessRecords.AnyAsync());
    }

    [Theory]
    [InlineData("C-001")]
    [InlineData("UNKNOWN")]
    public async Task FailureAfterInsertRollsBackAndDoesNotReturnDecision(string code)
    {
        await using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<SgcaeDbContext>(options => options.AddInterceptors(new FailAfterAccessInsert()))));
        using var failingClient = failingFactory.CreateClient();
        using var response = await failingClient.PostAsJsonAsync(Route, new RegisterAccessRequest(code, pointId, MovementType.Entry));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Fallo simulado", await response.Content.ReadAsStringAsync());
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessRecords.AnyAsync());
    }

    [Fact]
    public async Task TimestampComesFromInjectedServerClock()
    {
        var instant = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        await using var timedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(instant));
        }));
        using var timedClient = timedFactory.CreateClient();
        using var response = await timedClient.PostAsJsonAsync(Route, new RegisterAccessRequest("C-001", pointId, MovementType.Exit));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(instant, (await response.Content.ReadFromJsonAsync<AccessRecordDto>(JsonOptions))!.OccurredAt);
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    private sealed class FailAfterAccessInsert : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AccessRecord>().Any())
                throw new InvalidOperationException("Fallo simulado después del INSERT.");
            return ValueTask.FromResult(result);
        }
    }
}
