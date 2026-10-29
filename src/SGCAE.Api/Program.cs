using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using SGCAE.Api;
using SGCAE.Api.Errors;
using SGCAE.Infrastructure;
using SGCAE.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEmployeeUseCases();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Sgcae"));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SgcaeDbContext>("sql-server", tags: ["database"]);

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
});

app.Run();

public partial class Program;
