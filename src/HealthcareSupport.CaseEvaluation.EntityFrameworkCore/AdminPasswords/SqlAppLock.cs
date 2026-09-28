using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 -- the cross-process lock, built on SQL Server application locks
/// (<c>sys.sp_getapplock</c> / <c>sp_releaseapplock</c>), which apply to SQL Server and to Azure SQL
/// Database alike.
///
/// <para><b>Why not ABP's distributed lock.</b> The API and the AuthServer both register Medallion's
/// Redis provider, but the DbMigrator registers NONE -- and the migrator is one of the two processes
/// that races here. Using the ABP lock would mean adding a Redis registration, Redis connectivity
/// and Redis credentials to the migrator. An application lock needs nothing new in either process:
/// both already connect to the same HOST database, which is where the lock is taken, so they contend
/// correctly by construction.</para>
///
/// <para><b>Why a dedicated, unpooled connection.</b> The lock owner is <c>Session</c>, and SQL
/// Server releases a session-owned lock when the session logs out. With pooling ON, closing the
/// connection returns it to the pool and the SESSION stays alive -- so the lock would survive a
/// crash between acquire and release, for the lifetime of the pooled connection. With
/// <c>Pooling=false</c>, disposing really does log out, which makes the release a belt-and-braces
/// step rather than the only thing standing between a crash and a stuck lock.</para>
/// </summary>
public sealed class SqlAppLock : IAdminPasswordStoreLock, ITransientDependency
{
    /// <summary>
    /// Namespaced so these can never collide with an application lock taken for another purpose in
    /// the same database. Resource names are compared as binary, so case matters and this prefix is
    /// fixed lowercase.
    /// </summary>
    private const string ResourcePrefix = "admin-password:";

    /// <summary>
    /// Long enough to outlast a slow first creation (a Key Vault round trip on a cold identity),
    /// short enough that a genuinely stuck lock fails a deployment rather than hanging it.
    /// </summary>
    public const int DefaultLockTimeoutMilliseconds = 30_000;

    private readonly string _connectionString;
    private readonly int _lockTimeoutMilliseconds;

    public SqlAppLock(IConfiguration configuration)
        : this(HostConnectionStringFrom(configuration), DefaultLockTimeoutMilliseconds)
    {
    }

    /// <summary>
    /// The primitive form. Public rather than hidden behind an internals-visible attribute: a lock
    /// timeout is an ordinary operational parameter, and a test that cannot choose one can only
    /// prove the wait, never the refusal.
    /// </summary>
    public SqlAppLock(string connectionString, int lockTimeoutMilliseconds)
    {
        Check.NotNullOrWhiteSpace(connectionString, nameof(connectionString));

        _lockTimeoutMilliseconds = lockTimeoutMilliseconds;
        _connectionString = new SqlConnectionStringBuilder(connectionString)
        {
            // See the class remarks: this is what makes dispose release the lock.
            Pooling = false,
        }.ConnectionString;
    }

    private static string HostConnectionStringFrom(IConfiguration configuration)
    {
        Check.NotNull(configuration, nameof(configuration));

        var configured = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new AbpException(
                "ConnectionStrings:Default is required to take the admin-password lock. Both the " +
                "migrator and the API reach the host database, and the lock is taken there so they " +
                "contend on the same resource.");
        }

        return configured;
    }

    public async Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        var resource = ResourcePrefix + name;
        var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await AcquireOrThrowAsync(connection, resource, _lockTimeoutMilliseconds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new Handle(connection, resource);
    }

    private static async Task AcquireOrThrowAsync(
        SqlConnection connection,
        string resource,
        int lockTimeoutMilliseconds,
        CancellationToken cancellationToken)
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
            command.Parameters.AddWithValue("@timeout", lockTimeoutMilliseconds);

            // The statement's own timeout must outlast the lock timeout, or the client gives up
            // first and reports a command timeout instead of the lock's own return code.
            command.CommandTimeout = (lockTimeoutMilliseconds / 1000) + 30;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var code = (int)result.Value;

            // 0 granted, 1 granted after waiting. Everything else is a refusal: -1 timeout,
            // -2 cancelled, -3 deadlock victim, -999 call error. None may be treated as success --
            // proceeding without the lock is what produces two passwords for one account.
            if (code is 0 or 1)
            {
                return;
            }

            throw new AbpException(
                "Could not take the admin-password lock for resource " + resource + ": sp_getapplock " +
                "returned " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " (-1 timeout, -2 cancelled, -3 deadlock victim, -999 call error). No password was " +
                "read or written.");
        }
    }

    private sealed class Handle : IAsyncDisposable
    {
        private readonly SqlConnection _connection;
        private readonly string _resource;

        public Handle(SqlConnection connection, string resource)
        {
            _connection = connection;
            _resource = resource;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                var command = new SqlCommand(
                    "EXEC sys.sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';",
                    _connection);

                await using (command.ConfigureAwait(false))
                {
                    command.Parameters.AddWithValue("@resource", _resource);
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                // Unconditional, and the real guarantee. Disposing an unpooled connection logs the
                // session out, and SQL Server frees a session-owned lock at logout -- so the lock is
                // released even when the explicit release above throws.
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
