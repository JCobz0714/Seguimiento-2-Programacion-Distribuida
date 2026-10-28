using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SGCAE.Infrastructure;
using SGCAE.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Sgcae"));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SgcaeDbContext>("sql-server", tags: ["database"]);

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
});

app.Run();
