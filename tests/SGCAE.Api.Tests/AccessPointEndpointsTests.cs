using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGCAE.Api.Contracts;
using SGCAE.Application.AccessPoints;
using SGCAE.Infrastructure.Persistence;

namespace SGCAE.Api.Tests;

public sealed class AccessPointEndpointsTests(SgcaeApiFactory factory) : IClassFixture<SgcaeApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/access-points";
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        // Cada clase tiene su propia base temporal y ejecuta sus casos en serie.
        await using var context = factory.Database.CreateContext();
        await context.AccessPoints.ExecuteDeleteAsync();
        client = factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<AccessPointDto> CreateAsync(string name, bool enabled = true)
    {
        using var response = await client.PostAsJsonAsync(Route, new CreateAccessPointRequest(name, enabled));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccessPointDto>())!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)status, problem!.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegistrationPersistsNameAndStateAndReturnsLocation(bool enabled)
    {
        using var response = await client.PostAsJsonAsync(Route, new CreateAccessPointRequest(" Principal ", enabled));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var point = (await response.Content.ReadFromJsonAsync<AccessPointDto>())!;
        Assert.NotEqual(Guid.Empty, point.Id);
        Assert.Equal("Principal", point.Name);
        Assert.Equal(enabled, point.IsEnabled);
        Assert.EndsWith($"{Route}/{point.Id}", response.Headers.Location!.ToString());
        Assert.Equal(point, await client.GetFromJsonAsync<AccessPointDto>(response.Headers.Location));

        await using var context = factory.Database.CreateContext();
        var stored = await context.AccessPoints.SingleAsync();
        Assert.Equal(point.Id, stored.Id);
        Assert.Equal("Principal", stored.Name);
        Assert.Equal(enabled, stored.IsEnabled);
    }

    [Fact]
    public async Task OmittedStateDefaultsToEnabled()
    {
        using var response = await client.PostAsJsonAsync(Route, new { name = "Principal" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var point = (await response.Content.ReadFromJsonAsync<AccessPointDto>())!;
        Assert.True(point.IsEnabled);
        await using var context = factory.Database.CreateContext();
        Assert.True((await context.AccessPoints.SingleAsync()).IsEnabled);
    }

    [Fact]
    public async Task MaximumLengthNameAndDuplicateNamesAreAllowed()
    {
        var name = new string('A', 100);
        var first = await CreateAsync(name);
        var second = await CreateAsync(name, false);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(name, second.Name);
        await using var context = factory.Database.CreateContext();
        Assert.Equal(2, await context.AccessPoints.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task MissingNameReturnsBadRequest(string? name)
    {
        using var response = await client.PostAsJsonAsync(Route, new { name });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessPoints.AnyAsync());
    }

    [Fact]
    public async Task NameOverLimitDoesNotPersist()
    {
        using var response = await client.PostAsJsonAsync(Route, new { name = new string('A', 101) });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessPoints.AnyAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"name\":\"Principal\",\"isEnabled\":null}")]
    [InlineData("{\"name\":\"Principal\",\"isEnabled\":\"true\"}")]
    [InlineData("{\"name\":\"Principal\",\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    public async Task InvalidBodyReturnsBadRequest(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route, content);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.AccessPoints.AnyAsync());
    }

    [Fact]
    public async Task UnknownPointReturnsNotFound()
    {
        using var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyListUsesDefaultPagination()
    {
        var result = (await client.GetFromJsonAsync<AccessPointPage>(Route))!;
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FilterAppliesToItemsAndTotalBeforePagination(bool enabled)
    {
        await CreateAsync("C", enabled);
        await CreateAsync("B", !enabled);
        await CreateAsync("A", enabled);
        var query = $"{Route}?isEnabled={enabled}&pageSize=1";
        var first = (await client.GetFromJsonAsync<AccessPointPage>($"{query}&page=1"))!;
        var second = (await client.GetFromJsonAsync<AccessPointPage>($"{query}&page=2"))!;
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, second.TotalCount);
        Assert.Equal(1, first.PageSize);
        Assert.Equal(2, second.Page);
        Assert.Equal("A", Assert.Single(first.Items).Name);
        Assert.Equal("C", Assert.Single(second.Items).Name);
        Assert.All(first.Items.Concat(second.Items), point => Assert.Equal(enabled, point.IsEnabled));
        var beyond = (await client.GetFromJsonAsync<AccessPointPage>($"{query}&page=3"))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(2, beyond.TotalCount);
        var all = (await client.GetFromJsonAsync<AccessPointPage>(Route))!;
        Assert.Equal(3, all.TotalCount);
        Assert.Contains(all.Items, point => point.IsEnabled);
        Assert.Contains(all.Items, point => !point.IsEnabled);
    }

    [Fact]
    public async Task DuplicateNamesHaveStableNonOverlappingPages()
    {
        await CreateAsync("Principal");
        await CreateAsync("Principal", false);
        Guid[] expected;
        await using (var context = factory.Database.CreateContext())
        {
            // Orden de Guid de SQL Server; no coincide necesariamente con Guid.CompareTo.
            expected = await context.AccessPoints.OrderBy(point => point.Name).ThenBy(point => point.Id)
                .Select(point => point.Id).ToArrayAsync();
        }
        var page1 = (await client.GetFromJsonAsync<AccessPointPage>($"{Route}?pageSize=1"))!;
        var page2 = (await client.GetFromJsonAsync<AccessPointPage>($"{Route}?pageSize=1&page=2"))!;
        Assert.Equal(expected[0], Assert.Single(page1.Items).Id);
        Assert.Equal(expected[1], Assert.Single(page2.Items).Id);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=abc")]
    [InlineData("isEnabled=active")]
    public async Task InvalidQueryReturnsBadRequest(string query)
    {
        using var response = await client.GetAsync($"{Route}?{query}");
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QueriesDoNotTrackOrChangePointState()
    {
        var point = await CreateAsync("Secundario", false);
        using var scope = factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAccessPointQueries>();
        var context = scope.ServiceProvider.GetRequiredService<SgcaeDbContext>();
        Assert.Equal(point, await queries.GetByIdAsync(point.Id, default));
        Assert.Equal(point, Assert.Single((await queries.ListAsync(1, 20, false, default)).Items));
        Assert.Empty(context.ChangeTracker.Entries());
        await using var verification = factory.Database.CreateContext();
        Assert.False((await verification.AccessPoints.SingleAsync()).IsEnabled);
    }
}
