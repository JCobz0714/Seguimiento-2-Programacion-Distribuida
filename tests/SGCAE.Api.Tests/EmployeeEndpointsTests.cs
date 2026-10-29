using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGCAE.Api.Contracts;
using SGCAE.Application.Employees;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.Services;
using SGCAE.Domain.ValueObjects;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Api.Tests;

public sealed class EmployeeEndpointsTests(EmployeeApiFactory factory) : IClassFixture<EmployeeApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/employees";
    private HttpClient client = null!;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static CreateEmployeeRequest NewEmployee(string identification = "00123") =>
        new(identification, "Ana", "Pérez", "ana@example.com", "Operadora", "Bodega");

    private static UpdateEmployeeRequest UpdatedEmployee() =>
        new("María", "Gómez", "maria@example.com", "Supervisora", "Logística");

    public async Task InitializeAsync()
    {
        // La clase se ejecuta en serie; todas las solicitudes usan su base aleatoria.
        await using var context = factory.Database.CreateContext();
        await context.AccessRecords.ExecuteDeleteAsync();
        await context.Credentials.ExecuteDeleteAsync();
        await context.AccessPoints.ExecuteDeleteAsync();
        await context.Employees.ExecuteDeleteAsync();
        client = factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<EmployeeDto> CreateAsync(string identification = "00123")
    {
        using var response = await client.PostAsJsonAsync(Route, NewEmployee(identification));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(JsonOptions))!;
    }

    private async Task<EmployeeDto> GetAsync(Guid id)
    {
        using var response = await client.GetAsync($"{Route}/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(JsonOptions))!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)expected, problem!.Status);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateReturnsLocationAndPersistedNormalizedEmployee()
    {
        using var response = await client.PostAsJsonAsync(Route,
            new CreateEmployeeRequest(" 00123 ", " Ana ", " Pérez ", " ana@example.com ", " Operadora ", " Bodega "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<EmployeeDto>(JsonOptions))!;
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(EmploymentStatus.Active, created.Status);
        Assert.EndsWith($"{Route}/{created.Id}", response.Headers.Location!.ToString());
        Assert.Equal(created, await client.GetFromJsonAsync<EmployeeDto>(response.Headers.Location, JsonOptions));
        await using var context = factory.Database.CreateContext();
        var stored = await context.Employees.SingleAsync();
        Assert.Equal("00123", stored.Identification.Value);
        Assert.Equal("Ana", stored.FirstName);
        Assert.Equal("Pérez", stored.LastName);
        Assert.Equal("ana@example.com", stored.Email.Value);
        Assert.Equal("Operadora", stored.JobTitle);
        Assert.Equal("Bodega", stored.OrganizationalArea);
    }

    [Fact]
    public async Task UpdateAndRepeatedDeactivationPreserveIdentityAndInactiveStatus()
    {
        var employee = await CreateAsync();
        using var updatedResponse = await client.PutAsJsonAsync($"{Route}/{employee.Id}", UpdatedEmployee());
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await GetAsync(employee.Id);
        Assert.Equal("María", updated.FirstName);
        Assert.Equal("Gómez", updated.LastName);
        Assert.Equal("maria@example.com", updated.Email);
        Assert.Equal("Supervisora", updated.JobTitle);
        Assert.Equal("Logística", updated.OrganizationalArea);
        Assert.Equal(employee.Identification, updated.Identification);
        Assert.Equal(EmploymentStatus.Active, updated.Status);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PatchAsync($"{Route}/{employee.Id}/deactivate", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        using var secondUpdate = await client.PutAsJsonAsync($"{Route}/{employee.Id}",
            UpdatedEmployee() with { JobTitle = "Coordinadora" });
        Assert.Equal(HttpStatusCode.OK, secondUpdate.StatusCode);
        var inactive = await GetAsync(employee.Id);
        Assert.Equal(EmploymentStatus.Inactive, inactive.Status);
        Assert.Equal("Coordinadora", inactive.JobTitle);
        Assert.Equal(employee.Identification, inactive.Identification);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(EmploymentStatus.Inactive, (await context.Employees.SingleAsync()).Status);
    }

    [Fact]
    public async Task DeactivationPreservesCredentialsAndAccessHistory()
    {
        var employee = await CreateAsync();
        Guid recordId;
        await using (var context = factory.Database.CreateContext())
        {
            var stored = await context.Employees.SingleAsync();
            var credential = new Credential(stored, new CredentialCode("C-001"));
            var point = new AccessPoint("Principal");
            var record = new AccessPolicy().Evaluate(credential.Code, point.Id, MovementType.Entry,
                DateTimeOffset.UtcNow, credential, stored, point);
            recordId = record.Id;
            context.AddRange(credential, point, record);
            await context.SaveChangesAsync();
        }
        using var response = await client.PatchAsync($"{Route}/{employee.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verification = factory.Database.CreateContext();
        Assert.Single(await verification.Credentials.ToListAsync());
        var preserved = await verification.AccessRecords.SingleAsync();
        Assert.Equal(recordId, preserved.Id);
        Assert.Equal(AccessResult.Authorized, preserved.Result);
        Assert.Equal(employee.Id, preserved.EmployeeId);
    }

    [Theory]
    [InlineData("00123")]
    [InlineData(" 00123 ")]
    public async Task DuplicateIdentificationReturnsConflict(string duplicate)
    {
        await CreateAsync();
        using var response = await client.PostAsJsonAsync(Route, NewEmployee(duplicate));
        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(1, await context.Employees.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRegistrationsWithSameIdentificationCreateOnlyOneEmployee()
    {
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(Route, NewEmployee("RACE-01")),
            client.PostAsJsonAsync(Route, NewEmployee("RACE-01")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
            await using var context = factory.Database.CreateContext();
            Assert.Equal(1, await context.Employees.CountAsync());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task IdentificationComparisonIsCaseSensitiveAndEmailIsNotUnique()
    {
        var first = await CreateAsync("ABC");
        var second = await CreateAsync("abc");
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Email, second.Email);
    }

    public static IEnumerable<object[]> InvalidCreateRequests()
    {
        yield return [NewEmployee() with { Identification = null! }];
        yield return [NewEmployee() with { Identification = " " }];
        yield return [NewEmployee() with { Identification = new string('1', 31) }];
        yield return [NewEmployee() with { FirstName = null! }];
        yield return [NewEmployee() with { FirstName = new string('A', 101) }];
        yield return [NewEmployee() with { LastName = " " }];
        yield return [NewEmployee() with { Email = "invalid" }];
        yield return [NewEmployee() with { Email = "Ana <ana@example.com>" }];
        yield return [NewEmployee() with { Email = new string('a', 243) + "@example.com" }];
        yield return [NewEmployee() with { JobTitle = " " }];
        yield return [NewEmployee() with { OrganizationalArea = new string('A', 101) }];
    }

    [Theory]
    [MemberData(nameof(InvalidCreateRequests))]
    public async Task InvalidCreateReturnsBadRequestAndDoesNotPersist(CreateEmployeeRequest request)
    {
        using var response = await client.PostAsJsonAsync(Route, request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.Employees.AnyAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"identification\":\"00123\",\"firstName\":\"Ana\",\"lastName\":\"Pérez\",\"email\":\"ana@example.com\",\"jobTitle\":\"Operadora\",\"organizationalArea\":\"Bodega\",\"status\":\"Inactive\"}")]
    public async Task MalformedOrUnsupportedCreateBodyReturnsBadRequest(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route, content);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.Employees.AnyAsync());
    }

    [Fact]
    public async Task InvalidUpdateDoesNotPartiallyChangeEmployee()
    {
        var employee = await CreateAsync();
        using var response = await client.PutAsJsonAsync($"{Route}/{employee.Id}",
            UpdatedEmployee() with { OrganizationalArea = new string('X', 101) });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(employee, await GetAsync(employee.Id));
    }

    [Theory]
    [InlineData("identification", "other")]
    [InlineData("status", "Inactive")]
    public async Task UpdateRejectsChangesToIdentityOrStatus(string field, string value)
    {
        var employee = await CreateAsync();
        var body = JsonSerializer.SerializeToNode(UpdatedEmployee(), JsonOptions)!;
        body[field] = value;
        using var response = await client.PutAsJsonAsync($"{Route}/{employee.Id}", body);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(employee, await GetAsync(employee.Id));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task UnknownEmployeeReturnsNotFound(string method)
    {
        var route = $"{Route}/{Guid.NewGuid()}";
        using var response = method switch
        {
            "PUT" => await client.PutAsJsonAsync(route, UpdatedEmployee()),
            "PATCH" => await client.PatchAsync($"{route}/deactivate", null),
            _ => await client.GetAsync(route)
        };
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListUsesStablePaginationAndIncludesInactiveEmployees()
    {
        await CreateAsync("003");
        var first = await CreateAsync("001");
        await CreateAsync("002");
        using var deactivation = await client.PatchAsync($"{Route}/{first.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivation.StatusCode);
        var page1 = (await client.GetFromJsonAsync<EmployeePage>($"{Route}?page=1&pageSize=2", JsonOptions))!;
        var page2 = (await client.GetFromJsonAsync<EmployeePage>($"{Route}?page=2&pageSize=2", JsonOptions))!;
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(new[] { "001", "002" }, page1.Items.Select(employee => employee.Identification));
        Assert.Equal(EmploymentStatus.Inactive, page1.Items[0].Status);
        Assert.Equal("003", Assert.Single(page2.Items).Identification);
        var beyond = (await client.GetFromJsonAsync<EmployeePage>($"{Route}?page=3&pageSize=2", JsonOptions))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(3, beyond.TotalCount);
    }

    [Fact]
    public async Task EmptyListHasDefaultPagination()
    {
        var page = (await client.GetFromJsonAsync<EmployeePage>(Route, JsonOptions))!;
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=abc")]
    public async Task InvalidPaginationReturnsBadRequest(string query)
    {
        using var response = await client.GetAsync($"{Route}?{query}");
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QueriesDoNotTrackEntities()
    {
        var employee = await CreateAsync();
        using var scope = factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IEmployeeQueries>();
        var context = scope.ServiceProvider.GetRequiredService<SgcaeDbContext>();
        Assert.Equal(employee, await queries.GetByIdAsync(employee.Id, default));
        Assert.Single((await queries.ListAsync(1, 20, default)).Items);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
