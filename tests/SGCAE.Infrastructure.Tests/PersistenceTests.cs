using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.Services;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Infrastructure.Tests;

public sealed class PersistenceTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    private static Employee Employee(string identification = "00123") =>
        new(new EmployeeIdentification(identification), "Ana", "Pérez",
            new EmailAddress("ana@example.com"), "Operadora", "Bodega");

    private static readonly DateTimeOffset Timestamp =
        new(2026, 10, 28, 16, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public async Task MigrationCanBeAppliedAgainAndMatchesCurrentModel()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task EntitiesAndAuthorizedHistorySurviveReloadAndStateChanges()
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var employee = Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Recepción");
        var record = new AccessPolicy().Evaluate(credential.Code, point.Id, MovementType.Entry,
            Timestamp, credential, employee, point);
        context.AddRange(employee, credential, point, record);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loadedEmployee = await context.Employees.SingleAsync(e => e.Id == employee.Id);
        var loadedCredential = await context.Credentials.SingleAsync(c => c.Id == credential.Id);
        var loadedPoint = await context.AccessPoints.SingleAsync(p => p.Id == point.Id);
        Assert.Equal(employee.Identification, loadedEmployee.Identification);
        Assert.Equal(employee.Email, loadedEmployee.Email);
        Assert.Equal(employee.FirstName, loadedEmployee.FirstName);
        Assert.Equal(employee.LastName, loadedEmployee.LastName);
        Assert.Equal(employee.JobTitle, loadedEmployee.JobTitle);
        Assert.Equal(employee.OrganizationalArea, loadedEmployee.OrganizationalArea);
        Assert.Equal(employee.Id, loadedCredential.EmployeeId);
        Assert.Equal(credential.Code, loadedCredential.Code);
        Assert.Equal(point.Name, loadedPoint.Name);

        loadedEmployee.Deactivate();
        loadedCredential.Revoke();
        loadedPoint.Disable();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(EmploymentStatus.Inactive, (await context.Employees.SingleAsync()).Status);
        Assert.Equal(CredentialStatus.Revoked, (await context.Credentials.SingleAsync()).Status);
        Assert.False((await context.AccessPoints.SingleAsync()).IsEnabled);
        var loadedRecord = await context.AccessRecords.SingleAsync();
        Assert.Equal(record.Id, loadedRecord.Id);
        Assert.Equal(AccessResult.Authorized, loadedRecord.Result);
        Assert.Null(loadedRecord.RejectionReason);
        Assert.Equal(employee.Id, loadedRecord.EmployeeId);
        Assert.Equal(credential.Id, loadedRecord.CredentialId);
        Assert.Equal(point.Id, loadedRecord.AccessPointId);
        Assert.Equal(point.Id, loadedRecord.RequestedAccessPointId);
        Assert.Equal(credential.Code, loadedRecord.RequestedCredentialCode);
        Assert.Equal(Timestamp, loadedRecord.OccurredAt);
        Assert.Equal(TimeSpan.Zero, loadedRecord.OccurredAt.Offset);
    }

    public static IEnumerable<object[]> RejectionCases =>
        from reason in Enum.GetValues<AccessRejectionReason>()
        from movement in Enum.GetValues<MovementType>()
        select new object[] { reason, movement };

    [Theory]
    [MemberData(nameof(RejectionCases))]
    public async Task RejectedAttemptsIncludingUnknownReferencesSurviveReload(
        AccessRejectionReason reason, MovementType movement)
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        Employee? employee = Employee();
        Credential? credential = new(employee, new CredentialCode("C1"));
        AccessPoint? point = new("Bodega");
        var pointId = point.Id;
        var code = credential.Code;

        if (reason == AccessRejectionReason.UnknownCredential)
        {
            credential = null;
            employee = null;
        }
        else if (reason == AccessRejectionReason.EmployeeNotFound)
        {
            // La FK exige el propietario persistido aunque la consulta de aplicación no lo resuelva.
            context.Add(employee);
            employee = null;
        }
        else
        {
            if (reason == AccessRejectionReason.InactiveEmployee) employee.Deactivate();
            context.Add(employee);
        }

        if (credential is not null)
        {
            if (reason == AccessRejectionReason.BlockedCredential) credential.Block();
            if (reason == AccessRejectionReason.RevokedCredential) credential.Revoke();
            context.Add(credential);
        }
        if (reason == AccessRejectionReason.UnknownAccessPoint)
            point = null;
        else
        {
            if (reason == AccessRejectionReason.DisabledAccessPoint) point.Disable();
            context.Add(point);
        }

        var record = new AccessPolicy().Evaluate(code, pointId, movement, Timestamp,
            credential, employee, point);
        context.Add(record);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var loaded = await context.AccessRecords.SingleAsync();
        Assert.Equal(AccessResult.Rejected, loaded.Result);
        Assert.Equal(reason, loaded.RejectionReason);
        Assert.Equal(movement, loaded.Movement);
        Assert.Equal(code, loaded.RequestedCredentialCode);
        Assert.Equal(pointId, loaded.RequestedAccessPointId);
        Assert.Equal(employee?.Id, loaded.EmployeeId);
        Assert.Equal(credential?.Id, loaded.CredentialId);
        Assert.Equal(point?.Id, loaded.AccessPointId);
    }

    [Fact]
    public async Task DuplicateEmployeeIdentificationIsRejectedBySqlServer()
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Add(Employee(" 00123 "));
        await context.SaveChangesAsync();
        context.Add(Employee("00123"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(2601, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task DuplicateCredentialCodeAcrossEmployeesIsRejectedBySqlServer()
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var first = Employee("1");
        var second = Employee("2");
        context.AddRange(first, second, new Credential(first, new CredentialCode("C1")));
        await context.SaveChangesAsync();
        context.Add(new Credential(second, new CredentialCode(" C1 ")));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(2601, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task UniqueColumnsAndQueriesRespectCaseSensitiveValueEquality()
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var upper = Employee("AB");
        var lower = Employee("ab");
        var upperCode = new CredentialCode("Key");
        var lowerCode = new CredentialCode("key");
        context.AddRange(upper, lower, new Credential(upper, upperCode), new Credential(lower, lowerCode));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var id = new EmployeeIdentification("AB");
        Assert.Equal(upper.Id, (await context.Employees.SingleAsync(e => e.Identification == id)).Id);
        Assert.Equal(upper.Id, (await context.Credentials.SingleAsync(c => c.Code == upperCode)).EmployeeId);
        Assert.Equal(lower.Id, (await context.Credentials.SingleAsync(c => c.Code == lowerCode)).EmployeeId);
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("credential")]
    [InlineData("point")]
    public async Task HistoryPreventsDeletionOfReferencedEntities(string entity)
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var employee = Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Recepción");
        var record = new AccessPolicy().Evaluate(credential.Code, point.Id,
            MovementType.Entry, Timestamp, credential, employee, point);
        context.AddRange(employee, credential, point, record);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Cargar solo la entidad eliminada comprueba la FK real, no el seguimiento de EF.
        switch (entity)
        {
            case "employee": context.Remove(await context.Employees.SingleAsync()); break;
            case "credential": context.Remove(await context.Credentials.SingleAsync()); break;
            default: context.Remove(await context.AccessPoints.SingleAsync()); break;
        }
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task CredentialCannotReferenceMissingEmployee()
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Add(new Credential(Employee(), new CredentialCode("C1")));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("credential")]
    [InlineData("movement")]
    [InlineData("result")]
    public async Task DatabaseRejectsStatesOutsideDomainEvenIfTrackingIsBypassed(string target)
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var employee = Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Recepción");
        var record = new AccessPolicy().Evaluate(credential.Code, point.Id,
            MovementType.Entry, Timestamp, credential, employee, point);
        context.AddRange(employee, credential, point, record);
        switch (target)
        {
            case "employee": context.Entry(employee).Property(e => e.Status).CurrentValue = (EmploymentStatus)99; break;
            case "credential": context.Entry(credential).Property(c => c.Status).CurrentValue = (CredentialStatus)99; break;
            case "movement": context.Entry(record).Property(r => r.Movement).CurrentValue = (MovementType)99; break;
            default: context.Entry(record).Property(r => r.Result).CurrentValue = AccessResult.Rejected; break;
        }
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
    }
}
