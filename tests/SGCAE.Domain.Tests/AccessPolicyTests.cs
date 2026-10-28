using SGCAE.Domain.Common;
using SGCAE.Domain.Entities;
using SGCAE.Domain.Enums;
using SGCAE.Domain.Services;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Domain.Tests;

public class AccessPolicyTests
{
    private readonly AccessPolicy policy = new();

    [Theory]
    [InlineData(MovementType.Entry)]
    [InlineData(MovementType.Exit)]
    public void ActiveEmployeeAndCredentialAtEnabledPointAreAuthorized(MovementType movement)
    {
        var employee = TestData.Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Recepción");
        var record = policy.Evaluate(credential.Code, point.Id, movement, TestData.Timestamp,
            credential, employee, point);

        Assert.Equal(AccessResult.Authorized, record.Result);
        Assert.Null(record.RejectionReason);
        Assert.Equal(employee.Id, record.EmployeeId);
        Assert.Equal(credential.Id, record.CredentialId);
        Assert.Equal(point.Id, record.AccessPointId);
        Assert.Equal(movement, record.Movement);
        Assert.Equal(TestData.Timestamp.UtcDateTime, record.OccurredAt.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, record.OccurredAt.Offset);

        // Cambiar el estado actual no altera el resultado histórico.
        employee.Deactivate();
        credential.Revoke();
        point.Disable();
        Assert.Equal(AccessResult.Authorized, record.Result);
    }

    public static IEnumerable<object[]> RejectionCases =>
        from reason in Enum.GetValues<AccessRejectionReason>()
        from movement in Enum.GetValues<MovementType>()
        select new object[] { reason, movement };

    [Theory]
    [MemberData(nameof(RejectionCases))]
    public void EveryBusinessRejectionProducesTraceableRecord(
        AccessRejectionReason reason, MovementType movement)
    {
        Employee? employee = TestData.Employee();
        var code = new CredentialCode("C1");
        Credential? credential = new(employee, code);
        AccessPoint? point = new("Bodega");
        var pointId = point.Id;

        switch (reason)
        {
            case AccessRejectionReason.UnknownCredential:
                credential = null;
                employee = null;
                break;
            case AccessRejectionReason.EmployeeNotFound: employee = null; break;
            case AccessRejectionReason.InactiveEmployee: employee.Deactivate(); break;
            case AccessRejectionReason.BlockedCredential: credential.Block(); break;
            case AccessRejectionReason.RevokedCredential: credential.Revoke(); break;
            case AccessRejectionReason.UnknownAccessPoint: point = null; break;
            case AccessRejectionReason.DisabledAccessPoint: point.Disable(); break;
        }

        var record = policy.Evaluate(code, pointId, movement, TestData.Timestamp,
            credential, employee, point);

        Assert.Equal(AccessResult.Rejected, record.Result);
        Assert.Equal(reason, record.RejectionReason);
        Assert.Equal(code, record.RequestedCredentialCode);
        Assert.Equal(pointId, record.RequestedAccessPointId);
        Assert.Equal(credential?.Id, record.CredentialId);
        Assert.Equal(employee?.Id, record.EmployeeId);
        Assert.Equal(point?.Id, record.AccessPointId);
        Assert.Equal(movement, record.Movement);
        Assert.Equal(TestData.Timestamp, record.OccurredAt);
        Assert.NotEqual(Guid.Empty, record.Id);
    }

    [Fact]
    public void MultipleFailuresUseDocumentedPriorityAndAllowRepeatedAttempts()
    {
        var employee = TestData.Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Bodega", false);
        employee.Deactivate();
        credential.Revoke();

        var first = policy.Evaluate(credential.Code, point.Id, MovementType.Entry,
            TestData.Timestamp, credential, employee, point);
        var second = policy.Evaluate(credential.Code, point.Id, MovementType.Entry,
            TestData.Timestamp, credential, employee, point);

        Assert.Equal(AccessRejectionReason.InactiveEmployee, first.RejectionReason);
        Assert.Equal(first.RejectionReason, second.RejectionReason);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void ConsecutiveAuthorizedEntriesAreAllowed()
    {
        var employee = TestData.Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Bodega");
        for (var i = 0; i < 2; i++)
        {
            var record = policy.Evaluate(credential.Code, point.Id, MovementType.Entry,
                TestData.Timestamp.AddMinutes(i), credential, employee, point);
            Assert.Equal(AccessResult.Authorized, record.Result);
        }
    }

    [Fact]
    public void ResolvedObjectsMustMatchRequestedCodePointAndEmployee()
    {
        var employee = TestData.Employee();
        var credential = new Credential(employee, new CredentialCode("C1"));
        var point = new AccessPoint("Bodega");

        Assert.Throws<DomainException>(() => policy.Evaluate(new CredentialCode("OTHER"), point.Id,
            MovementType.Entry, TestData.Timestamp, credential, employee, point));
        Assert.Throws<DomainException>(() => policy.Evaluate(credential.Code, Guid.NewGuid(),
            MovementType.Entry, TestData.Timestamp, credential, employee, point));
        Assert.Throws<DomainException>(() => policy.Evaluate(credential.Code, point.Id,
            MovementType.Entry, TestData.Timestamp, credential, TestData.Employee(), point));
        Assert.Throws<DomainException>(() => policy.Evaluate(credential.Code, point.Id,
            MovementType.Entry, TestData.Timestamp, null, employee, point));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void UndefinedMovementIsInvalidInput(int movement)
    {
        Assert.Throws<DomainException>(() => policy.Evaluate(new CredentialCode("C1"),
            Guid.NewGuid(), (MovementType)movement, TestData.Timestamp, null, null, null));
    }

    [Fact]
    public void EmptyPointAndMissingTimestampAreInvalidInput()
    {
        var code = new CredentialCode("C1");
        Assert.Throws<DomainException>(() => policy.Evaluate(code, Guid.Empty,
            MovementType.Entry, TestData.Timestamp, null, null, null));
        Assert.Throws<DomainException>(() => policy.Evaluate(code, Guid.NewGuid(),
            MovementType.Entry, default, null, null, null));
    }
}
