using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SGCAE.Testing;

namespace SGCAE.Api.Tests;

public sealed class SgcaeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public SqlServerFixture Database { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Sgcae", Database.ConnectionString);
    }

    public Task InitializeAsync() => Database.InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await Database.DisposeAsync();
    }
}
