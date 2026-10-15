using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11f (2026-05-04) -- pure-unit tests for
/// <see cref="ConfirmationNumberRetryPolicy"/>. Verifies retry semantics
/// without materialising a real <see cref="DbUpdateException"/> or SQL
/// connection by injecting a fake collision predicate.
/// </summary>
public class ConfirmationNumberRetryPolicyUnitTests
{
    [Fact]
    public async Task RunWithRetry_OperationSucceedsFirstAttempt_DoesNotRetry()
    {
        var attempts = 0;

        var result = await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
            () =>
            {
                attempts++;
                return Task.FromResult("A00001");
            },
            maxAttempts: 5,
            isCollision: _ => true);

        attempts.ShouldBe(1);
        result.ShouldBe("A00001");
    }

    [Fact]
    public async Task RunWithRetry_TransientCollision_RetriesAndSucceeds()
    {
        var attempts = 0;

        var result = await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("UNIQUE constraint failed");
                }
                return Task.FromResult("A00003");
            },
            maxAttempts: 5);

        attempts.ShouldBe(3);
        result.ShouldBe("A00003");
    }

    [Fact]
    public async Task RunWithRetry_NonCollisionException_PropagatesImmediately()
    {
        var attempts = 0;

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("Some other unrelated error.");
                },
                maxAttempts: 5));

        attempts.ShouldBe(1);
        ex.Message.ShouldBe("Some other unrelated error.");
    }

    [Fact]
    public async Task RunWithRetry_PersistentCollision_SurfacesAfterBudgetExhausted()
    {
        var attempts = 0;

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("UNIQUE constraint failed");
                },
                maxAttempts: 3));

        attempts.ShouldBe(3);
        ex.Message.ShouldContain("after 3 attempts");
        ex.InnerException.ShouldNotBeNull();
        ex.InnerException!.Message.ShouldBe("UNIQUE constraint failed");
    }

    [Fact]
    public async Task RunWithRetry_CustomCollisionPredicate_UsesPredicateNotDefault()
    {
        var attempts = 0;
        var triggerMessage = "MY-CUSTOM-COLLISION";

        var result = await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
            () =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new InvalidOperationException(triggerMessage);
                }
                return Task.FromResult("A00002");
            },
            maxAttempts: 5,
            isCollision: ex => ex.Message == triggerMessage);

        attempts.ShouldBe(2);
        result.ShouldBe("A00002");
    }

    [Fact]
    public async Task RunWithRetry_NullOperation_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
                operation: null!,
                maxAttempts: 5));
    }

    [Fact]
    public async Task RunWithRetry_ZeroAttempts_Throws()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
                () => Task.FromResult("A00001"),
                maxAttempts: 0));
    }

    [Fact]
    public async Task RunWithRetry_NegativeAttempts_Throws()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await ConfirmationNumberRetryPolicy.RunWithRetryAsync<string>(
                () => Task.FromResult("A00001"),
                maxAttempts: -1));
    }

    [Fact]
    public async Task RunWithRetry_VoidOverload_Works()
    {
        var attempts = 0;

        await ConfirmationNumberRetryPolicy.RunWithRetryAsync(
            () =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new InvalidOperationException("UNIQUE constraint failed");
                }
                return Task.CompletedTask;
            },
            maxAttempts: 5);

        attempts.ShouldBe(2);
    }

    [Theory]
    [InlineData("UNIQUE constraint failed: AppEntity_Appointments.IX_TenantId_RequestConfirmationNumber", true)]
    [InlineData("Violation of UNIQUE KEY constraint 'IX_Foo'.", true)]
    [InlineData("Cannot insert duplicate key in object 'dbo.AppEntity_Appointments'.", true)]
    [InlineData("FK constraint violation", false)]
    [InlineData("Connection timeout", false)]
    [InlineData("", false)]
    public void IsUniqueConstraintViolation_DetectsKnownMessages(string message, bool expected)
    {
        var ex = new InvalidOperationException(message);
        ConfirmationNumberRetryPolicy.IsUniqueConstraintViolation(ex).ShouldBe(expected);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WalksInnerExceptionChain()
    {
        var inner = new InvalidOperationException("UNIQUE constraint failed");
        var middle = new Exception("wrapper", inner);
        var outer = new InvalidOperationException("top-level", middle);

        ConfirmationNumberRetryPolicy.IsUniqueConstraintViolation(outer).ShouldBeTrue();
    }

    [Fact]
    public void IsUniqueConstraintViolation_NullException_ReturnsFalse()
    {
        ConfirmationNumberRetryPolicy.IsUniqueConstraintViolation(null).ShouldBeFalse();
    }

    [Fact]
    public void IsCollisionFromEfCore_NonDbUpdateException_ReturnsFalse()
    {
        var ex = new InvalidOperationException("UNIQUE constraint failed");

        // Even though the message looks like a collision, the gate
        // requires the outer type name to be DbUpdateException. This
        // guards against accidental retry of unrelated exceptions that
        // happen to mention "unique" in their message.
        ConfirmationNumberRetryPolicy.IsCollisionFromEfCore(ex).ShouldBeFalse();
    }

    [Fact]
    public void IsCollisionFromEfCore_NullException_ReturnsFalse()
    {
        ConfirmationNumberRetryPolicy.IsCollisionFromEfCore(null).ShouldBeFalse();
    }
}
