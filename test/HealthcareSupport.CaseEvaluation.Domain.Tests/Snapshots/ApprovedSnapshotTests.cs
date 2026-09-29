using System;
using System.IO;
using System.Text;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Snapshots;

/// <summary>
/// The shared approved-snapshot helper's contract. Every snapshot gate leans on it, so a helper
/// that passed on a difference would silence all of them at once.
/// </summary>
public sealed class ApprovedSnapshotTests : IDisposable
{
    private const string Approved = "unchanged-line\nremoved-line\n";
    private const string Changed = "unchanged-line\nadded-line\n";

    private static readonly ApprovedSnapshotWording Wording = new(
        Headline: "The sample surface changed.",
        IssueReference: "#1",
        RemovedMeaning: "a sample line went away");

    private readonly string _directory;

    public ApprovedSnapshotTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "hcs-approved-snapshot-" + Guid.NewGuid().ToString("N"));
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

    private string ApprovedPath => Path.Combine(_directory, "sample.approved.txt");

    private string ReceivedPath => Path.Combine(_directory, "sample.received.txt");

    [Fact]
    public void Equal_text_passes_and_writes_no_received_file()
    {
        File.WriteAllText(ApprovedPath, Approved, new UTF8Encoding(false));

        ApprovedSnapshot.AssertMatches(Approved, ApprovedPath, Wording);

        File.Exists(ReceivedPath).ShouldBeFalse("a matching snapshot must leave nothing to copy across");
    }

    [Fact]
    public void A_difference_fails_naming_every_changed_line_and_writes_the_received_file()
    {
        File.WriteAllText(ApprovedPath, Approved, new UTF8Encoding(false));

        var failure = Should.Throw<ShouldAssertException>(
            () => ApprovedSnapshot.AssertMatches(Changed, ApprovedPath, Wording));

        failure.Message.ShouldStartWith("The sample surface changed.\n\n");
        failure.Message.ShouldContain("change was intended (#1). Read the lines below.");
        failure.Message.ShouldContain($"  cp \"{ReceivedPath}\" \"{ApprovedPath}\"");
        failure.Message.ShouldContain("Gone from the approved surface (a sample line went away):\n  - removed-line\n");
        failure.Message.ShouldContain("New in the actual surface:\n  + added-line\n");
        failure.Message.ShouldNotContain("unchanged-line", customMessage: "an unchanged line must not be reported");

        File.ReadAllText(ReceivedPath, Encoding.UTF8).ShouldBe(Changed);
    }

    [Fact]
    public void A_line_ending_difference_alone_still_fails()
    {
        // A CRLF approved file must not compare equal to LF text: that is how a gate goes green
        // on Windows and red in CI for a reason nobody can see in the diff.
        File.WriteAllText(ApprovedPath, Approved.Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false));

        Should.Throw<ShouldAssertException>(
            () => ApprovedSnapshot.AssertMatches(Approved, ApprovedPath, Wording));
    }

    [Fact]
    public void A_missing_approved_file_fails_naming_its_path()
    {
        var failure = Should.Throw<ShouldAssertException>(
            () => ApprovedSnapshot.AssertMatches(Approved, ApprovedPath, Wording));

        failure.Message.ShouldContain(ApprovedPath);
    }

    [Fact]
    public void A_path_without_the_approved_suffix_is_refused()
    {
        var wrongName = Path.Combine(_directory, "sample.txt");

        Should.Throw<ArgumentException>(
            () => ApprovedSnapshot.AssertMatches(Approved, wrongName, Wording));
    }
}
