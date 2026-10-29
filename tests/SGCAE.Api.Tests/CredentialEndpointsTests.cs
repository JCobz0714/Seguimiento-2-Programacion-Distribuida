using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGCAE.Api.Contracts;
using SGCAE.Application.Credentials;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.Services;
using SGCAE.Domain.ValueObjects;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Api.Tests;

public sealed class CredentialEndpointsTests(SgcaeApiFactory factory) : IClassFixture<SgcaeApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/credentials";
    private HttpClient client = null!;
    private Guid employeeId;
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
        var employee = Employee("001");
        context.Add(employee);
        await context.SaveChangesAsync();
        employeeId = employee.Id;
        client = factory.CreateClient();
    }

    private static Employee Employee(string identification) => new(new EmployeeIdentification(identification),
        "Ana", "Pérez", new EmailAddress("ana@example.com"), "Operadora", "Bodega");

    public Task DisposeAsync() { client.Dispose(); return Task.CompletedTask; }

    private async Task<CredentialDto> AssignAsync(string code = "C-001", Guid? owner = null)
    {
        using var response = await client.PostAsJsonAsync(Route, new AssignCredentialRequest(owner ?? employeeId, code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CredentialDto>(JsonOptions))!;
    }

    private async Task<CredentialDto> GetAsync(Guid id) =>
        (await client.GetFromJsonAsync<CredentialDto>($"{Route}/{id}", JsonOptions))!;

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        Assert.DoesNotContain("SqlException", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AssignmentReturnsLocationAndPersistsNormalizedCodeAndOwner()
    {
        using var response = await client.PostAsJsonAsync(Route, new AssignCredentialRequest(employeeId, " C-001 "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var credential = (await response.Content.ReadFromJsonAsync<CredentialDto>(JsonOptions))!;
        Assert.Equal(employeeId, credential.EmployeeId);
        Assert.Equal("C-001", credential.Code);
        Assert.Equal(CredentialStatus.Active, credential.Status);
        Assert.EndsWith($"{Route}/{credential.Id}", response.Headers.Location!.ToString());
        Assert.Equal(credential, await GetAsync(credential.Id));
        await using var context = factory.Database.CreateContext();
        Assert.Equal(credential, CredentialDto.From(await context.Credentials.SingleAsync()));
    }

    [Fact]
    public async Task InactiveEmployeeCannotReceiveCredential()
    {
        using var deactivation = await client.PatchAsync($"/api/employees/{employeeId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivation.StatusCode);
        using var response = await client.PostAsJsonAsync(Route, new AssignCredentialRequest(employeeId, "C-001"));
        await ProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.Credentials.AnyAsync());
    }

    [Fact]
    public async Task MissingEmployeeReturnsNotFound()
    {
        using var response = await client.PostAsJsonAsync(Route, new AssignCredentialRequest(Guid.NewGuid(), "C-001"));
        await ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task InvalidCodeDoesNotPersist(string? code)
    {
        using var response = await client.PostAsJsonAsync(Route, new { employeeId, code });
        await ProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.Credentials.AnyAsync());
    }

    [Fact]
    public async Task CodeLengthBoundaryAndCaseSensitivityAllowMultipleCredentials()
    {
        await AssignAsync(new string('A', 100));
        await AssignAsync("ABC");
        await AssignAsync("abc");
        using var invalid = await client.PostAsJsonAsync(Route, new AssignCredentialRequest(employeeId, new string('A', 101)));
        await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(3, await context.Credentials.CountAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"code\":\"C-001\"}")]
    [InlineData("{\"employeeId\":\"bad\",\"code\":\"C-001\"}")]
    [InlineData("{\"employeeId\":\"00000000-0000-0000-0000-000000000001\",\"code\":\"C-001\",\"status\":\"Revoked\"}")]
    public async Task InvalidBodiesReturnBadRequest(string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route, content);
        await ProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DuplicateCodeCannotBeReusedAfterRevocation()
    {
        var credential = await AssignAsync();
        using var revoked = await client.PatchAsync($"{Route}/{credential.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using var response = await client.PostAsJsonAsync(Route, new AssignCredentialRequest(employeeId, " C-001 "));
        await ProblemAsync(response, HttpStatusCode.Conflict);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(1, await context.Credentials.CountAsync());
    }

    [Fact]
    public async Task ConcurrentAssignmentsToDifferentEmployeesEnforceGlobalUniqueness()
    {
        var other = Employee("002");
        await using (var context = factory.Database.CreateContext())
        {
            context.Add(other);
            await context.SaveChangesAsync();
        }
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(Route, new AssignCredentialRequest(employeeId, "DUP")),
            client.PostAsJsonAsync(Route, new AssignCredentialRequest(other.Id, "DUP")));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            await ProblemAsync(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
            await using var context = factory.Database.CreateContext();
            Assert.Equal(1, await context.Credentials.CountAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task BlockAndRevokeAreRepeatableAndRevocationIsTerminal()
    {
        var credential = await AssignAsync();
        foreach (var operation in new[] { "block", "block", "revoke", "revoke" })
        {
            using var response = await client.PatchAsync($"{Route}/{credential.Id}/{operation}", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = (await response.Content.ReadFromJsonAsync<CredentialDto>(JsonOptions))!;
            Assert.Equal(operation == "block" ? CredentialStatus.Blocked : CredentialStatus.Revoked, updated.Status);
            Assert.Equal(credential.EmployeeId, updated.EmployeeId);
            Assert.Equal(credential.Code, updated.Code);
        }
        using var blocked = await client.PatchAsync($"{Route}/{credential.Id}/block", null);
        await ProblemAsync(blocked, HttpStatusCode.BadRequest);
        Assert.Equal(CredentialStatus.Revoked, (await GetAsync(credential.Id)).Status);
    }

    [Fact]
    public async Task ConcurrentBlockAndRevokeAlwaysLeaveCredentialRevoked()
    {
        var credential = await AssignAsync();
        var responses = await Task.WhenAll(
            client.PatchAsync($"{Route}/{credential.Id}/block", null),
            client.PatchAsync($"{Route}/{credential.Id}/revoke", null));
        try
        {
            Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });
            Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
            Assert.Equal(CredentialStatus.Revoked, (await GetAsync(credential.Id)).Status);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task RevokePreservesHistoricalAccessAndWorksForInactiveOwner()
    {
        var credential = await AssignAsync();
        Guid historyId;
        await using (var context = factory.Database.CreateContext())
        {
            var employee = await context.Employees.SingleAsync();
            var stored = await context.Credentials.SingleAsync();
            var point = new AccessPoint("Principal");
            var history = new AccessPolicy().Evaluate(stored.Code, point.Id, MovementType.Entry,
                DateTimeOffset.UtcNow, stored, employee, point);
            historyId = history.Id;
            context.AddRange(point, history);
            employee.Deactivate();
            await context.SaveChangesAsync();
        }
        using var response = await client.PatchAsync($"{Route}/{credential.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = factory.Database.CreateContext();
        var record = await verification.AccessRecords.SingleAsync();
        Assert.Equal(historyId, record.Id);
        Assert.Equal(AccessResult.Authorized, record.Result);
        Assert.Equal(credential.Id, record.CredentialId);
        Assert.Equal(CredentialStatus.Revoked, (await verification.Credentials.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("block")]
    [InlineData("revoke")]
    public async Task MissingCredentialReturnsNotFound(string operation)
    {
        var url = $"{Route}/{Guid.NewGuid()}";
        using var response = operation == "get" ? await client.GetAsync(url) : await client.PatchAsync($"{url}/{operation}", null);
        await ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListFiltersOwnerAndStatusBeforePagination()
    {
        await AssignAsync("003");
        var first = await AssignAsync("001");
        var second = await AssignAsync("002");
        using var block1 = await client.PatchAsync($"{Route}/{first.Id}/block", null);
        using var block2 = await client.PatchAsync($"{Route}/{second.Id}/block", null);
        Assert.Equal(HttpStatusCode.OK, block1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, block2.StatusCode);
        var other = Employee("002");
        await using (var context = factory.Database.CreateContext())
        {
            context.Add(other);
            await context.SaveChangesAsync();
        }
        var otherCredential = await AssignAsync("000", other.Id);
        using var block3 = await client.PatchAsync($"{Route}/{otherCredential.Id}/block", null);
        var query = $"{Route}?employeeId={employeeId}&status=Blocked&pageSize=1";
        var page1 = (await client.GetFromJsonAsync<CredentialPage>(query, JsonOptions))!;
        var page2 = (await client.GetFromJsonAsync<CredentialPage>($"{query}&page=2", JsonOptions))!;
        Assert.Equal(2, page1.TotalCount);
        Assert.Equal("001", Assert.Single(page1.Items).Code);
        Assert.Equal("002", Assert.Single(page2.Items).Code);
        Assert.All(page1.Items.Concat(page2.Items), c => { Assert.Equal(employeeId, c.EmployeeId); Assert.Equal(CredentialStatus.Blocked, c.Status); });
        var beyond = (await client.GetFromJsonAsync<CredentialPage>($"{query}&page=3", JsonOptions))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(2, beyond.TotalCount);
        Assert.Equal(4, (await client.GetFromJsonAsync<CredentialPage>(Route, JsonOptions))!.TotalCount);
    }

    [Fact]
    public async Task EmptyListAndUnknownOwnerFilterReturnEmptyPages()
    {
        var page = (await client.GetFromJsonAsync<CredentialPage>(Route, JsonOptions))!;
        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.TotalCount);
        await AssignAsync();
        Assert.Empty((await client.GetFromJsonAsync<CredentialPage>($"{Route}?employeeId={Guid.NewGuid()}", JsonOptions))!.Items);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("status=999")]
    [InlineData("status=Unknown")]
    [InlineData("employeeId=bad")]
    [InlineData("employeeId=00000000-0000-0000-0000-000000000000")]
    public async Task InvalidQueryReturnsBadRequest(string query)
    {
        using var response = await client.GetAsync($"{Route}?{query}");
        await ProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QueriesAreNotTracked()
    {
        var credential = await AssignAsync();
        using var scope = factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<ICredentialQueries>();
        Assert.Equal(credential, await queries.GetByIdAsync(credential.Id, default));
        Assert.Single((await queries.ListAsync(1, 20, null, null, default)).Items);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<SgcaeDbContext>().ChangeTracker.Entries());
    }
}
