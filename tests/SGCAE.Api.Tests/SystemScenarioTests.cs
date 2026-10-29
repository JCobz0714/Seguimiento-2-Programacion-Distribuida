using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGCAE.Api.Contracts;
using SGCAE.Application.AccessPoints;
using SGCAE.Application.AccessRecords;
using SGCAE.Application.Credentials;
using SGCAE.Application.Employees;
using SGCAE.Domain.Enums;

namespace SGCAE.Api.Tests;

// Los datos del negocio se crean por HTTP, desde una base migrada vacía.
public sealed class SystemScenarioTests(SgcaeApiFactory factory) : IClassFixture<SgcaeApiFactory>, IAsyncLifetime
{
    private HttpClient client = null!;
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
        client = factory.CreateClient();
    }

    public Task DisposeAsync() { client.Dispose(); return Task.CompletedTask; }

    private async Task<T> CreateAsync<T>(string route, object body)
    {
        using var response = await client.PostAsJsonAsync(route, body, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private Task<EmployeeDto> EmployeeAsync(string id = "001") => CreateAsync<EmployeeDto>("/api/employees",
        new CreateEmployeeRequest(id, "Ana", "Pérez", "ana@example.com", "Operadora", "Bodega"));
    private Task<AccessPointDto> PointAsync(bool enabled = true) => CreateAsync<AccessPointDto>("/api/access-points",
        new CreateAccessPointRequest("Principal", enabled));
    private Task<CredentialDto> CredentialAsync(Guid employee, string code) => CreateAsync<CredentialDto>("/api/credentials",
        new AssignCredentialRequest(employee, code));
    private Task<AccessRecordDto> AccessAsync(string code, Guid point, MovementType movement = MovementType.Entry) =>
        CreateAsync<AccessRecordDto>("/api/access-records", new RegisterAccessRequest(code, point, movement));

    private async Task PatchAsync(string route, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PatchAsync(route, null);
        Assert.Equal(expected, response.StatusCode);
    }

    private async Task<AccessRecordPage> HistoryAsync(string query = "")
    {
        using var response = await client.GetAsync("/api/access-records" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccessRecordPage>(JsonOptions))!;
    }

    [Fact]
    public async Task CompleteLifecyclePreservesEveryDecisionAndSupportsCombinedHistoryFilters()
    {
        var from = DateTimeOffset.UtcNow;
        var employee = await EmployeeAsync();
        using var update = await client.PutAsJsonAsync($"/api/employees/{employee.Id}",
            new UpdateEmployeeRequest("Ana", "Pérez", "ana@example.com", "Supervisora", "Logística"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await client.GetFromJsonAsync<EmployeeDto>($"/api/employees/{employee.Id}", JsonOptions))!;
        Assert.Equal("Supervisora", updated.JobTitle);
        Assert.Equal(employee.Identification, updated.Identification);

        var point = await PointAsync();
        var disabled = await PointAsync(false);
        var enabledPoints = (await client.GetFromJsonAsync<AccessPointPage>("/api/access-points?isEnabled=true", JsonOptions))!;
        Assert.Equal(point.Id, Assert.Single(enabledPoints.Items).Id);
        var credential = await CredentialAsync(employee.Id, "C-001");
        var expected = new List<AccessRecordDto>
        {
            await AccessAsync(credential.Code, point.Id),
            await AccessAsync(credential.Code, point.Id, MovementType.Exit),
            await AccessAsync(credential.Code, disabled.Id)
        };
        Assert.All(expected.Take(2), r => Assert.Equal(AccessResult.Authorized, r.Result));
        Assert.Equal(AccessRejectionReason.DisabledAccessPoint, expected[2].RejectionReason);

        await PatchAsync($"/api/credentials/{credential.Id}/block");
        expected.Add(await AccessAsync(credential.Code, point.Id));
        Assert.Equal(AccessRejectionReason.BlockedCredential, expected[^1].RejectionReason);
        await PatchAsync($"/api/credentials/{credential.Id}/revoke");
        expected.Add(await AccessAsync(credential.Code, point.Id, MovementType.Exit));
        Assert.Equal(AccessRejectionReason.RevokedCredential, expected[^1].RejectionReason);

        var replacement = await CredentialAsync(employee.Id, "C-002");
        await PatchAsync($"/api/employees/{employee.Id}/deactivate", HttpStatusCode.NoContent);
        expected.Add(await AccessAsync(replacement.Code, point.Id));
        Assert.Equal(AccessRejectionReason.InactiveEmployee, expected[^1].RejectionReason);
        expected.Add(await AccessAsync("UNKNOWN", point.Id));
        Assert.Equal(AccessRejectionReason.UnknownCredential, expected[^1].RejectionReason);
        Assert.Null(expected[^1].EmployeeId);
        var other = await EmployeeAsync("002");
        var otherCredential = await CredentialAsync(other.Id, "C-003");
        var missingPoint = Guid.NewGuid();
        expected.Add(await AccessAsync(otherCredential.Code, missingPoint));
        Assert.Equal(AccessRejectionReason.UnknownAccessPoint, expected[^1].RejectionReason);
        Assert.Null(expected[^1].AccessPointId);

        var history = await HistoryAsync();
        Assert.Equal(8, history.TotalCount);
        Assert.Equal(expected.Select(r => r.Id).Order(), history.Items.Select(r => r.Id).Order());
        foreach (var record in history.Items) Assert.Equal(expected.Single(r => r.Id == record.Id), record);
        var query = $"?employeeId={employee.Id}&accessPointId={point.Id}&result=Rejected"
            + $"&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}&pageSize=2";
        var first = await HistoryAsync(query);
        var second = await HistoryAsync(query + "&page=2");
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Equal(expected.Where(r => r.EmployeeId == employee.Id && r.RequestedAccessPointId == point.Id
            && r.Result == AccessResult.Rejected).Select(r => r.Id).Order(),
            first.Items.Concat(second.Items).Select(r => r.Id).Order());
        await using var database = factory.Database.CreateContext();
        Assert.Equal(8, await database.AccessRecords.CountAsync());
        Assert.Equal(3, await database.Credentials.CountAsync());
        Assert.Equal(EmploymentStatus.Inactive, (await database.Employees.SingleAsync(e => e.Id == employee.Id)).Status);
    }

    [Fact]
    public async Task DuplicateAndInvalidOperationsDoNotLeavePartialDataInFullWorkflow()
    {
        var employee = await EmployeeAsync();
        using var duplicateEmployee = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeRequest(" 001 ", "Otra", "Persona", "otra@example.com", "Cargo", "Área"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmployee.StatusCode);
        var point = await PointAsync();
        var credential = await CredentialAsync(employee.Id, "C-001");
        using var duplicateCode = await client.PostAsJsonAsync("/api/credentials", new AssignCredentialRequest(employee.Id, " C-001 "));
        Assert.Equal(HttpStatusCode.Conflict, duplicateCode.StatusCode);
        using var invalid = await client.PostAsJsonAsync("/api/access-records", new RegisterAccessRequest("C-001", Guid.Empty, MovementType.Entry));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, (await HistoryAsync()).TotalCount);
        await AccessAsync(credential.Code, point.Id);
        await PatchAsync($"/api/employees/{employee.Id}/deactivate", HttpStatusCode.NoContent);
        using var rejectedAssignment = await client.PostAsJsonAsync("/api/credentials", new AssignCredentialRequest(employee.Id, "C-002"));
        Assert.Equal(HttpStatusCode.BadRequest, rejectedAssignment.StatusCode);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(1, await context.Employees.CountAsync());
        Assert.Equal(1, await context.Credentials.CountAsync());
        Assert.Equal(1, (await HistoryAsync()).TotalCount);
    }

    [Fact]
    public async Task ConcurrentAccessesAndRevocationLeaveConsistentHistory()
    {
        var employee = await EmployeeAsync();
        var point = await PointAsync();
        var credential = await CredentialAsync(employee.Id, "C-001");
        var attempts = Enumerable.Range(0, 4).Select(_ => AccessAsync(credential.Code, point.Id)).ToArray();
        var revocation = PatchAsync($"/api/credentials/{credential.Id}/revoke");
        var results = await Task.WhenAll(attempts);
        await revocation;
        var after = await AccessAsync(credential.Code, point.Id, MovementType.Exit);
        Assert.Equal(AccessRejectionReason.RevokedCredential, after.RejectionReason);
        Assert.All(results, r =>
        {
            Assert.True(r.Result == AccessResult.Authorized || r.RejectionReason == AccessRejectionReason.RevokedCredential);
            Assert.Equal(r.Result == AccessResult.Authorized, r.RejectionReason is null);
        });
        var history = await HistoryAsync();
        Assert.Equal(5, history.TotalCount);
        foreach (var record in results.Append(after))
            Assert.Equal(record, history.Items.Single(r => r.Id == record.Id));
    }

    [Theory]
    [InlineData("text/plain", false)]
    [InlineData("text/html", false)]
    [InlineData("text/plain", true)]
    [InlineData("text/html", true)]
    public async Task ErrorStatusAndProblemDetailsSurviveUnsupportedAcceptHeader(string accept, bool notFound)
    {
        using var request = new HttpRequestMessage(notFound ? HttpMethod.Get : HttpMethod.Post,
            notFound ? $"/api/employees/{Guid.NewGuid()}" : "/api/access-points");
        request.Headers.Accept.ParseAdd(accept);
        if (!notFound) request.Content = JsonContent.Create(new CreateAccessPointRequest(new string('X', 101)));
        using var response = await client.SendAsync(request);
        Assert.Equal(notFound ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)response.StatusCode, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
    }

    [Fact]
    public async Task RestartedApiReadsPreviouslyCommittedData()
    {
        var employee = await EmployeeAsync();
        var point = await PointAsync();
        var credential = await CredentialAsync(employee.Id, "C-001");
        var record = await AccessAsync(credential.Code, point.Id);
        await using var secondHost = factory.WithWebHostBuilder(_ => { });
        using var secondClient = secondHost.CreateClient();
        var employeeReloaded = await secondClient.GetFromJsonAsync<EmployeeDto>($"/api/employees/{employee.Id}", JsonOptions);
        Assert.Equal(employee, employeeReloaded);
        var history = (await secondClient.GetFromJsonAsync<AccessRecordPage>("/api/access-records", JsonOptions))!;
        Assert.Equal(record, Assert.Single(history.Items));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task DuplicateConflictKeepsItsStatusWithUnsupportedAccept(string accept)
    {
        await EmployeeAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/employees")
        {
            Content = JsonContent.Create(new CreateEmployeeRequest("001", "Ana", "Pérez",
                "ana@example.com", "Operadora", "Bodega"))
        };
        request.Headers.Accept.ParseAdd(accept);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(1, await context.Employees.CountAsync());
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task UnexpectedFailureReturnsSafeProblemDetailsWithUnsupportedAccept(string accept)
    {
        await using var failingHost = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IAccessPointRepository>(_ => throw new InvalidOperationException("detalle-interno-simulado"))));
        using var failingClient = failingHost.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-points")
        {
            Content = JsonContent.Create(new CreateAccessPointRequest("Principal"))
        };
        request.Headers.Accept.ParseAdd(accept);
        using var response = await failingClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(500, problem.Status);
        Assert.Equal("No fue posible completar la operación.", problem.Detail);
        Assert.DoesNotContain("detalle-interno-simulado", await response.Content.ReadAsStringAsync());
        await using var context = factory.Database.CreateContext();
        Assert.Equal(0, await context.AccessPoints.CountAsync());
    }
}
