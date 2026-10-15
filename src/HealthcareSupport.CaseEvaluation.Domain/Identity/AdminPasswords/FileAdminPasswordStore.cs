using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- the in-house server's store: one file per database in an operator-owned folder.
///
/// <para>Selected by <c>AdminPasswords:Directory</c>. On the server that folder is the git-ignored
/// <c>secrets/admin-passwords</c>, bind-mounted into both the migrator and the API, because either
/// may be the process that first creates a database.</para>
///
/// <para><b>Create-if-absent is atomic here, not merely checked.</b> The file is opened with
/// <see cref="FileMode.CreateNew"/>, which .NET maps to <c>O_CREAT | O_EXCL</c> on Unix, so exactly
/// one of any number of concurrent creators succeeds and the rest fail at the open. A
/// check-then-create would let two processes both pass the check and the second overwrite the first,
/// which is the failure this class exists to make impossible.</para>
///
/// <para><b>Why not <c>File.Move(overwrite: false)</c> into place:</b> on Unix .NET implements it as
/// an <c>lstat</c> of the destination followed by <c>rename</c>. A second writer landing between the
/// two is overwritten, so that shape reintroduces exactly the race <c>O_EXCL</c> removes.</para>
///
/// <para>The trailing newline is a completeness marker, not formatting. A creator that dies
/// mid-write leaves a file with no newline, and every later run then FAILS LOUDLY rather than
/// treating the truncated content as a password. Recovery is an operator reading that file; this
/// class never deletes one.</para>
/// </summary>
public sealed class FileAdminPasswordStore : IAdminPasswordStore
{
    /// <summary>How long a caller that lost the create race waits for the winner to finish writing.</summary>
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private const char CompletionMarker = '\n';

    private readonly string _directory;

    public FileAdminPasswordStore(string directory)
    {
        _directory = Check.NotNullOrWhiteSpace(directory, nameof(directory));
    }

    public async Task<string> GetOrCreateAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_directory, AdminPasswordNames.For(tenantId));

        try
        {
            return await CreateAsync(path, cancellationToken);
        }
        catch (IOException) when (File.Exists(path))
        {
            // ONLY "it is already there" counts as losing the race. Every other IOException -- a
            // read-only mount, a full disk, a missing directory (DirectoryNotFoundException derives
            // from IOException) -- leaves File.Exists false and propagates immediately, so a
            // misconfigured box reports its real cause at once instead of after a 5 second wait
            // and a misleading "incomplete" message. UnauthorizedAccessException is not an
            // IOException at all and never reaches here.
            return await ReadWhenCompleteAsync(path, cancellationToken);
        }
    }

    private static async Task<string> CreateAsync(string path, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            // Owner read/write only. The property is unsupported on Windows and throws if set there,
            // which is why this is guarded rather than applied unconditionally.
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var password = AdminPasswordPolicy.Generate();

        var stream = new FileStream(path, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(password + CompletionMarker), cancellationToken)
                .ConfigureAwait(false);

            // To DISK, not just out of the managed buffer. The next caller may be a different
            // process on the same box, and it reads the file rather than any shared buffer.
            stream.Flush(flushToDisk: true);
        }

        return password;
    }

    private static async Task<string> ReadWhenCompleteAsync(string path, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            var complete = TryReadComplete(path);
            if (complete != null)
            {
                return complete;
            }

            if (elapsed.Elapsed >= CompletionTimeout)
            {
                throw new AbpException(
                    "The stored admin password at " + path + " is incomplete: it has no terminating " +
                    "newline, so the process that created it did not finish writing. It has NOT been " +
                    "replaced, because overwriting it would separate the stored value from the " +
                    "password the account already has. Inspect the file and remove it only if no " +
                    "account is using it. Its content is not shown here on purpose.");
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The stored password, or null while the file is absent, still being written, or not yet
    /// terminated. An <see cref="IOException"/> here means the winner still holds the file with
    /// <see cref="FileShare.None"/>, which is indistinguishable from "not finished" and is treated
    /// as such; the bounded wait above turns a permanent version of it into a named error.
    /// </summary>
    private static string? TryReadComplete(string path)
    {
        string content;
        try
        {
            content = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }

        if (content.Length != AdminPasswordPolicy.GeneratedLength + 1 || content[^1] != CompletionMarker)
        {
            return null;
        }

        return content[..AdminPasswordPolicy.GeneratedLength];
    }
}
