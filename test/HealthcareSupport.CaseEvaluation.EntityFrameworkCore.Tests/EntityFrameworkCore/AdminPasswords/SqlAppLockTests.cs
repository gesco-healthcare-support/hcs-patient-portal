using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.MsSql;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// A real SQL Server for the admin-password lock. Application locks exist nowhere else -- the SQLite
/// rig has no <c>sp_getapplock</c> -- so an in-memory double could only ever re-assert the design
/// rather than test it. Started once per class from the image docker-compose.yml pins. Docker must
/// be running.
/// </summary>
public sealed class SqlAppLockFixture : IAsyncLifetime
{
    /// <summary>The image docker-compose.yml pins for sql-server.</summary>
    public const string Image = "mcr.microsoft.com/mssql/server:2022-CU25-GDR2-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }
}

/// <summary>
/// B12 task 4 -- the lock itself, against the engine it runs on.
/// </summary>
public sealed class SqlAppLockTests : IClassFixture<SqlAppLockFixture>
{
    private const string AResource = "admin-password-host";
    private const string ADifferentResource = "admin-password-office-3fa85f64-5717-4562-b3fc-2c963f66afa6";

    private readonly SqlAppLockFixture _sql;

    public SqlAppLockTests(SqlAppLockFixture sql)
    {
        _sql = sql;
    }

    private SqlAppLock Lock(int timeoutMilliseconds = SqlAppLock.DefaultLockTimeoutMilliseconds)
    {
        return new SqlAppLock(_sql.ConnectionString, timeoutMilliseconds);
    }

    [Fact]
    public async Task AnUncontendedLock_IsGranted()
    {
        var handle = await Lock().AcquireAsync(AResource);

        await handle.DisposeAsync();
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
            var other = await Lock(timeoutMilliseconds: 1000).AcquireAsync(ADifferentResource);
            await other.DisposeAsync();
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

        var unpooled = new SqlConnectionStringBuilder(_sql.ConnectionString) { Pooling = false }.ConnectionString;

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
