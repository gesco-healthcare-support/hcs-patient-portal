using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;
using Microsoft.Data.SqlClient;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 task 4 -- the lock itself, against the engine it runs on. Application locks exist nowhere
/// else: the SQLite rig has no <c>sp_getapplock</c>, so an in-memory double could only re-assert
/// the design rather than test it. Docker must be running.
///
/// <para>Uses the SHARED SQL Server container rather than starting one. A second container would be
/// created in parallel with the first and lose its race with the Docker daemon under any real
/// memory pressure, failing the feed tests -- which have nothing to do with this change -- with a
/// TaskCanceledException that looks nothing like its cause. See
/// <see cref="SqlServerCollection"/>.</para>
///
/// <para>Sharing a database with the feed tests is safe and is not merely tolerated: an application
/// lock is scoped to the database and keyed by resource NAME, every name here carries the
/// <c>admin-password:</c> prefix the production lock uses, and the feed tests take no application
/// locks at all. The collection also runs its classes sequentially.</para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SqlAppLockTests
{
    private const string AResource = "admin-password-host";
    private const string ADifferentResource = "admin-password-office-3fa85f64-5717-4562-b3fc-2c963f66afa6";

    private readonly SqlServerFeedFixture _sql;

    public SqlAppLockTests(SqlServerFeedFixture sql)
    {
        _sql = sql;
    }

    private SqlAppLock Lock(int timeoutMilliseconds = SqlAppLock.DefaultLockTimeoutMilliseconds)
    {
        return new SqlAppLock(_sql.FeedDatabase, timeoutMilliseconds);
    }

    /// <summary>
    /// The lock is really held in SQL Server while the handle lives, under the prefixed name, and
    /// really released when it is disposed. Asserted from a separate session, because "acquire
    /// returned" proves nothing on its own: an acquire that never reached sp_getapplock, or a
    /// dispose that released nothing, would both still return.
    /// </summary>
    [Fact]
    public async Task AnUncontendedLock_IsHeldInSqlServerUntilTheHandleIsDisposed()
    {
        var handle = await Lock().AcquireAsync(AResource);

        (await ProbeAsync(Prefixed(AResource))).ShouldBe(LockRefused, "the handle is still alive");

        await handle.DisposeAsync();

        (await ProbeAsync(Prefixed(AResource))).ShouldBe(LockGranted, "the handle was disposed");
    }

    /// <summary>
    /// The contended case, which is the one that matters: the second caller WAITS rather than being
    /// refused or, worse, granted alongside the first. It is granted only after the first releases.
    /// </summary>
    [Fact]
    public async Task ASecondCaller_WaitsAndIsGrantedAfterTheFirstReleases()
    {
        var first = await Lock().AcquireAsync(AResource);

        var second = Task.Run(async () => await Lock().AcquireAsync(AResource));

        // Still held: the second caller must not have completed.
        await Task.Delay(500);
        second.IsCompleted.ShouldBeFalse("the lock was still held");

        await first.DisposeAsync();

        var handle = await second.WaitAsync(TimeSpan.FromSeconds(30));
        await handle.DisposeAsync();
    }

    /// <summary>
    /// A caller that cannot get the lock in time THROWS, naming the resource and the return code.
    /// It must never fall through and read the store: two callers past this point is exactly the
    /// two-passwords-for-one-account outcome the lock exists to prevent.
    /// </summary>
    [Fact]
    public async Task ACallerThatTimesOut_ThrowsNamingTheResourceAndTheCode()
    {
        var held = await Lock().AcquireAsync(AResource);
        try
        {
            var elapsed = Stopwatch.StartNew();

            var thrown = await Should.ThrowAsync<AbpException>(
                async () => await Lock(timeoutMilliseconds: 1000).AcquireAsync(AResource));

            thrown.Message.ShouldContain(AResource);
            thrown.Message.ShouldContain("-1");
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
        }
        finally
        {
            await held.DisposeAsync();
        }
    }

    /// <summary>
    /// The control. Two different databases must not queue behind each other, or the first
    /// deployment of many offices serialises end to end for no reason.
    /// </summary>
    [Fact]
    public async Task ADifferentResource_IsNotBlocked()
    {
        var held = await Lock().AcquireAsync(AResource);
        try
        {
            // A 1000 ms lock timeout: had it queued behind the held lock, this would throw.
            var other = await Lock(timeoutMilliseconds: 1000).AcquireAsync(ADifferentResource);
            try
            {
                // Both held at once, each under its own name: the two did not share one resource.
                (await ProbeAsync(Prefixed(AResource))).ShouldBe(LockRefused);
                (await ProbeAsync(Prefixed(ADifferentResource))).ShouldBe(LockRefused);
            }
            finally
            {
                await other.DisposeAsync();
            }

            // Releasing one leaves the other held.
            (await ProbeAsync(Prefixed(ADifferentResource))).ShouldBe(LockGranted);
            (await ProbeAsync(Prefixed(AResource))).ShouldBe(LockRefused);
        }
        finally
        {
            await held.DisposeAsync();
        }
    }

    /// <summary>
    /// The guarantee the design rests on, asserted against SQL Server directly rather than through
    /// <see cref="SqlAppLock"/>.
    ///
    /// <para>A Session-owned lock is freed when the session logs out. With pooling ON, closing a
    /// connection returns it to the pool and the session SURVIVES -- so a process that crashed
    /// between acquire and release would leave the lock held for the pooled connection's lifetime,
    /// and nothing would ever release it. With <c>Pooling=false</c> the close is a real logout.</para>
    ///
    /// <para>This is why the lock opens its own unpooled connection instead of borrowing one. The
    /// explicit release is then belt and braces; this is the brace.</para>
    /// </summary>
    [Fact]
    public async Task AnUnpooledConnectionThatIsDisposedWithoutReleasing_StillFreesTheLock()
    {
        const string resource = "admin-password:unpooled-probe";

        var unpooled = new SqlConnectionStringBuilder(_sql.FeedDatabase) { Pooling = false }.ConnectionString;

        var holder = new SqlConnection(unpooled);
        await holder.OpenAsync();
        (await GetAppLockAsync(holder, resource, timeoutMilliseconds: 0)).ShouldBeOneOf(0, 1);

        // No sp_releaseapplock. Just a dispose, which on an unpooled connection is a logout.
        await holder.DisposeAsync();

        var next = new SqlConnection(unpooled);
        await using (next.ConfigureAwait(false))
        {
            await next.OpenAsync();
            (await GetAppLockAsync(next, resource, timeoutMilliseconds: 2000)).ShouldBeOneOf(0, 1);
        }
    }

    /// <summary>sp_getapplock's return codes the probes compare against.</summary>
    private const int LockGranted = 0;
    private const int LockRefused = -1;

    /// <summary>
    /// The production lock prefixes every name. Spelled out here rather than read from
    /// <see cref="SqlAppLock"/>, so a change to the prefix fails these tests instead of moving with
    /// them: callers on both processes must agree on the exact resource string.
    /// </summary>
    private static string Prefixed(string name) => "admin-password:" + name;

    /// <summary>
    /// Asks SQL Server, from a fresh session that waits for nothing, whether the resource is free.
    /// The connection is unpooled and disposed straight away, so a probe that IS granted releases
    /// its own lock at logout and never blocks the code under test.
    /// </summary>
    private async Task<int> ProbeAsync(string resource)
    {
        var unpooled = new SqlConnectionStringBuilder(_sql.FeedDatabase) { Pooling = false }.ConnectionString;
        var probe = new SqlConnection(unpooled);
        await using (probe.ConfigureAwait(false))
        {
            await probe.OpenAsync();
            return await GetAppLockAsync(probe, resource, timeoutMilliseconds: 0);
        }
    }

    private static async Task<int> GetAppLockAsync(SqlConnection connection, string resource, int timeoutMilliseconds)
    {
        var command = new SqlCommand(
            "EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Session', @LockTimeout = @timeout;",
            connection);

        await using (command.ConfigureAwait(false))
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(result);
            command.Parameters.AddWithValue("@resource", resource);
            command.Parameters.AddWithValue("@timeout", timeoutMilliseconds);

            await command.ExecuteNonQueryAsync();

            return (int)result.Value;
        }
    }
}
