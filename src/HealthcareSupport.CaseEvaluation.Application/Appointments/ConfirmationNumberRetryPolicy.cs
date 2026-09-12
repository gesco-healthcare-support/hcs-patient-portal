namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11f (2026-05-04) -- pure retry policy for the race-safe
/// confirmation-number generation path. Backs the unique index on
/// <c>Appointment(TenantId, RequestConfirmationNumber)</c> by retrying
/// the generate + insert sequence when a concurrent transaction wins
/// the unique-constraint race.
///
/// OLD did not have this guard -- two concurrent bookers could land on
/// the same number. NEW closes the race via:
///   (1) database-level unique index (DbContext fluent config), and
///   (2) this transient-collision retry loop in the AppService.
///
/// Extracted as <c>internal static</c> for unit-testability via
/// <c>InternalsVisibleTo</c>; the predicate
/// <see cref="IsUniqueConstraintViolation"/> is the seam tests use to
/// avoid needing a real DB context.
/// </summary>
internal static class ConfirmationNumberRetryPolicy
{
    /// <summary>
    /// SQL Server error number for primary-key constraint violations.
    /// Cite: https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors
    /// </summary>
    internal const int SqlServerPrimaryKeyViolationNumber = 2627;

    /// <summary>
    /// SQL Server error number for unique-index constraint violations.
    /// Cite: https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors
    /// </summary>
    internal const int SqlServerUniqueIndexViolationNumber = 2601;

    /// <summary>
    /// Default retry budget. The race window is microseconds wide so 5
    /// attempts comfortably cover a worst-case stampede; past that we
    /// surface the error rather than hide it.
    /// </summary>
    internal const int DefaultMaxAttempts = 5;

    /// <summary>
    /// Returns <c>true</c> when an exception thrown by EF Core's
    /// <c>SaveChangesAsync</c> represents a unique-constraint collision
    /// (primary-key or unique-index violation). Walks the inner-exception
    /// chain and inspects either a SqlException's <c>Number</c> or the
    /// message text as a fallback for non-SqlServer providers (sqlite
    /// in tests reports the violation as a generic exception with the
    /// substring "UNIQUE constraint failed").
    /// </summary>
    internal static bool IsUniqueConstraintViolation(Exception? ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            // SqlException is sealed against direct compile-time
            // reference here (the Application project intentionally
            // does not depend on Microsoft.Data.SqlClient). Inspect by
            // type name + reflective Number lookup so the policy is
            // provider-agnostic.
            var typeName = cur.GetType().FullName;
            if (typeName == "Microsoft.Data.SqlClient.SqlException"
                || typeName == "System.Data.SqlClient.SqlException")
            {
                var numberProp = cur.GetType().GetProperty("Number");
                if (numberProp?.GetValue(cur) is int number
                    && (number == SqlServerPrimaryKeyViolationNumber
                        || number == SqlServerUniqueIndexViolationNumber))
                {
                    return true;
                }
            }

            var msg = cur.Message ?? string.Empty;
            if (msg.IndexOf("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("duplicate key", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("violation of UNIQUE KEY constraint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Runs <paramref name="operation"/> up to <paramref name="maxAttempts"/>
    /// times, retrying when the thrown exception passes
    /// <paramref name="isCollision"/>. Any other exception is propagated
    /// immediately. The default <paramref name="isCollision"/> is
    /// <see cref="IsUniqueConstraintViolation"/>; tests inject a fake to
    /// avoid materialising a real <c>DbUpdateException</c>.
    /// </summary>
    /// <remarks>
    /// The operation is expected to be idempotent in the sense that
    /// re-running it produces a fresh confirmation number on each
    /// attempt; the retry loop intentionally does NOT add backoff
    /// because the conflict window is microseconds wide and a re-read
    /// of the MAX(...) immediately yields a higher number.
    /// </remarks>
    internal static async Task<T> RunWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = DefaultMaxAttempts,
        Func<Exception, bool>? isCollision = null)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                maxAttempts,
                "maxAttempts must be >= 1.");
        }

        var predicate = isCollision ?? IsUniqueConstraintViolation;

        Exception? lastCollision = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (predicate(ex))
            {
                lastCollision = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }
                // Continue to next attempt; no backoff (see remarks).
            }
        }

        throw new InvalidOperationException(
            $"Failed to insert appointment after {maxAttempts} attempts because the confirmation number kept colliding with concurrent bookings. The last collision is preserved as the inner exception.",
            lastCollision);
    }

    /// <summary>
    /// Convenience overload for the common case where the operation
    /// returns no value. Internally calls
    /// <see cref="RunWithRetryAsync{T}(Func{Task{T}}, int, Func{Exception, bool}?)"/>
    /// against a sentinel result.
    /// </summary>
    internal static Task RunWithRetryAsync(
        Func<Task> operation,
        int maxAttempts = DefaultMaxAttempts,
        Func<Exception, bool>? isCollision = null)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        return RunWithRetryAsync<object?>(
            async () => { await operation().ConfigureAwait(false); return null; },
            maxAttempts,
            isCollision);
    }

    /// <summary>
    /// Convenience gate that requires the outer exception to be of type
    /// <c>Microsoft.EntityFrameworkCore.DbUpdateException</c> before the
    /// inner-message scan runs. Compared by full type name to avoid
    /// adding a hard EF Core reference to the Application project.
    /// </summary>
    internal static bool IsCollisionFromEfCore(Exception? ex)
    {
        if (ex == null) return false;
        var typeName = ex.GetType().FullName;
        if (typeName != "Microsoft.EntityFrameworkCore.DbUpdateException"
            && typeName != "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException")
        {
            return false;
        }
        return IsUniqueConstraintViolation(ex);
    }
}
