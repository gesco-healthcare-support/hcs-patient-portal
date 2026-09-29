using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// B12 tasks 2 and 3 -- the folder store's contract, its create race, and the failures it must NOT
/// treat as a race.
/// </summary>
public sealed class FileAdminPasswordStoreTests : IDisposable
{
    private static readonly Guid AnOffice = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private readonly string _directory;

    public FileAdminPasswordStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "hcs-admin-pw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked temp directory is not worth failing a test over.
        }
    }

    private FileAdminPasswordStore Store(string? directory = null)
    {
        return new FileAdminPasswordStore(directory ?? _directory);
    }

    // ---- contract -------------------------------------------------------------------------

    [Fact]
    public async Task AnAbsentEntry_IsCreatedAndReturned()
    {
        var password = await Store().GetOrCreateAsync(null);

        password.Length.ShouldBe(AdminPasswordPolicy.GeneratedLength);
        File.Exists(Path.Combine(_directory, AdminPasswordNames.Host)).ShouldBeTrue();
    }

    [Fact]
    public async Task ASecondCall_ReturnsTheSameValue()
    {
        var store = Store();

        var first = await store.GetOrCreateAsync(null);
        var second = await store.GetOrCreateAsync(null);

        second.ShouldBe(first);
    }

    /// <summary>
    /// The decoy. A pre-existing entry is what the store finds on every run after the first, and on
    /// every run against a restored backup -- returning it unchanged is the whole point, so it is
    /// asserted against a value the store cannot have generated.
    /// </summary>
    [Fact]
    public async Task APreSeededEntry_IsReturnedUnchangedAndNeverOverwritten()
    {
        var decoy = new string('Z', AdminPasswordPolicy.GeneratedLength);
        var path = Path.Combine(_directory, AdminPasswordNames.Host);
        await File.WriteAllTextAsync(path, decoy + "\n", Encoding.UTF8);

        var returned = await Store().GetOrCreateAsync(null);

        returned.ShouldBe(decoy);
        (await File.ReadAllTextAsync(path)).ShouldBe(decoy + "\n");
    }

    [Fact]
    public async Task NothingIsEverDeleted()
    {
        var store = Store();
        await store.GetOrCreateAsync(null);
        await store.GetOrCreateAsync(AnOffice);

        await store.GetOrCreateAsync(null);

        Directory.GetFiles(_directory).Length.ShouldBe(2);
    }

    [Fact]
    public async Task EachDatabaseGetsItsOwnEntry()
    {
        var store = Store();

        var host = await store.GetOrCreateAsync(null);
        var office = await store.GetOrCreateAsync(AnOffice);

        office.ShouldNotBe(host);
        File.Exists(Path.Combine(_directory, AdminPasswordNames.For(AnOffice))).ShouldBeTrue();
    }

    // ---- the race -------------------------------------------------------------------------

    /// <summary>
    /// The reason this class uses an exclusive create rather than check-then-create. Sixteen
    /// creators start on an empty directory; all must agree, and exactly one file must exist. A
    /// check-then-create implementation fails this by returning several different passwords.
    /// </summary>
    [Fact]
    public async Task SixteenConcurrentCreators_AllReturnTheSameValueAndLeaveOneFile()
    {
        var store = Store();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => store.GetOrCreateAsync(null))));

        results.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
        Directory.GetFiles(_directory).Length.ShouldBe(1);
    }

    [Fact]
    public async Task NoTemporaryOrPartialFilesAreLeftBehind()
    {
        var store = Store();
        await store.GetOrCreateAsync(null);
        await store.GetOrCreateAsync(AnOffice);

        var expected = new[] { AdminPasswordNames.Host, AdminPasswordNames.For(AnOffice) }
            .OrderBy(n => n, StringComparer.Ordinal);

        Directory.GetFileSystemEntries(_directory)
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ShouldBe(expected);
    }

    // ---- failures that are NOT a race -----------------------------------------------------

    /// <summary>
    /// A creator that died mid-write leaves content with no terminating newline. That must fail
    /// loudly on every later run: silently replacing it would separate the stored value from the
    /// password the account already holds, and nothing would observe that until a sign-in failed.
    ///
    /// <para>The message names the FILE and must not contain what is in it.</para>
    /// </summary>
    [Fact]
    public async Task AHalfWrittenEntry_ThrowsNamingTheFileAndNotItsContent()
    {
        const string partial = "0123456789";
        var path = Path.Combine(_directory, AdminPasswordNames.Host);
        await File.WriteAllTextAsync(path, partial, Encoding.UTF8);

        var thrown = await Should.ThrowAsync<AbpException>(async () => await Store().GetOrCreateAsync(null));

        thrown.Message.ShouldContain(path);
        thrown.Message.ShouldNotContain(partial);
        (await File.ReadAllTextAsync(path)).ShouldBe(partial);
    }

    /// <summary>
    /// A missing directory is a misconfigured box, not a lost race, so it must surface at once with
    /// its real cause -- NOT after the five second completion wait, and not as an "incomplete file"
    /// error pointing the operator at a file that does not exist.
    /// </summary>
    [Fact]
    public async Task AMissingDirectory_FailsImmediatelyAndNamesThePath()
    {
        var missing = Path.Combine(_directory, "not-created");
        var elapsed = Stopwatch.StartNew();

        var thrown = await Should.ThrowAsync<DirectoryNotFoundException>(
            async () => await Store(missing).GetOrCreateAsync(null));

        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        thrown.Message.ShouldContain("not-created");
    }

    /// <summary>
    /// Owner-only permissions, asserted where they exist. The store sets them through
    /// UnixCreateMode, which is unsupported on Windows, so this is skipped there. CI runs Linux, so
    /// the assertion is exercised on every run that gates a merge.
    /// </summary>
    [Fact]
    public async Task OnUnix_TheEntryIsReadableOnlyByItsOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Store().GetOrCreateAsync(null);

        var mode = File.GetUnixFileMode(Path.Combine(_directory, AdminPasswordNames.Host));

        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    /// A directory the process cannot write is also a misconfiguration and must fail fast.
    /// UnauthorizedAccessException is not an IOException, so it never reaches the race branch at all
    /// -- this pins that, because widening the race branch to catch Exception would break it
    /// silently.
    /// </summary>
    [Fact]
    public async Task OnUnix_AnUnwritableDirectory_FailsImmediately()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var elapsed = Stopwatch.StartNew();

            await Should.ThrowAsync<UnauthorizedAccessException>(
                async () => await Store().GetOrCreateAsync(null));

            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        }
        finally
        {
            File.SetUnixFileMode(
                _directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
