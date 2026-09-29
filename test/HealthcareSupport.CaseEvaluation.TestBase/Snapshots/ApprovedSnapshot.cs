using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shouldly;

namespace HealthcareSupport.CaseEvaluation.Snapshots;

/// <summary>
/// The words one approved-snapshot gate uses in its failure message. Everything else in the
/// message is common to every gate, so no gate can drift from the others in how it tells a
/// developer to accept a change.
/// </summary>
/// <param name="Headline">The first line, naming what changed, for example "The authorization surface changed."</param>
/// <param name="IssueReference">The issue that asked for the gate, shown in brackets after "intended"; null for none.</param>
/// <param name="RemovedMeaning">What a line gone from the approved file means for this gate, shown in brackets.</param>
public sealed record ApprovedSnapshotWording(string Headline, string? IssueReference, string RemovedMeaning);

/// <summary>
/// Compares a rendered snapshot with its committed approved file and, on any difference, fails
/// with every removed and every added line plus the command that accepts the change.
///
/// WHY IT IS SHARED. The authorization surface (#707) and the generated documentation facts all
/// ask the same thing of a developer: read the difference, and if every line is intended, copy
/// the received file over the approved one in the same pull request. That instruction and the
/// compare behind it are one piece of knowledge; copying it into each gate would let one gate's
/// instructions drift from another's.
///
/// What it does NOT do: render, sort, or check line endings. Each gate owns its rendering, and so
/// its own determinism, and keeps its own LF assertion on the approved file.
/// </summary>
public static class ApprovedSnapshot
{
    /// <summary>The suffix every approved file carries; its received file swaps it for <see cref="ReceivedSuffix"/>.</summary>
    public const string ApprovedSuffix = ".approved.txt";

    /// <summary>The suffix of the file written beside the approved one when the two differ.</summary>
    public const string ReceivedSuffix = ".received.txt";

    /// <summary>
    /// The received file's path: beside the approved file, with the same stem. Deriving it rather
    /// than taking it as a parameter means a gate cannot write its received file somewhere the
    /// failure message does not point.
    /// </summary>
    public static string ReceivedPathFor(string approvedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedPath);

        if (!approvedPath.EndsWith(ApprovedSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"An approved snapshot must be named *{ApprovedSuffix} so its received file can sit " +
                $"beside it; got {approvedPath}",
                nameof(approvedPath));
        }

        return approvedPath[..^ApprovedSuffix.Length] + ReceivedSuffix;
    }

    /// <summary>
    /// Fails unless <paramref name="actual"/> equals the approved file byte for byte. On a
    /// difference it writes <paramref name="actual"/> to the received file first, so the command
    /// in the failure message has a file to copy.
    /// </summary>
    public static void AssertMatches(string actual, string approvedPath, ApprovedSnapshotWording wording)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(wording);
        var receivedPath = ReceivedPathFor(approvedPath);

        File.Exists(approvedPath).ShouldBeTrue(
            $"The approved snapshot is missing at {approvedPath}. It is the gate; without " +
            "it there is nothing to compare against.");

        // Read as raw bytes decoded without newline translation, so a file that had been
        // committed with CRLF fails loudly here rather than silently comparing equal on
        // Windows and unequal in CI.
        var approved = File.ReadAllText(approvedPath, Encoding.UTF8);

        if (!string.Equals(approved, actual, StringComparison.Ordinal))
        {
            File.WriteAllText(receivedPath, actual, new UTF8Encoding(false));

            throw new ShouldAssertException(BuildFailureMessage(approved, actual, approvedPath, wording));
        }
    }

    /// <summary>
    /// The failure text: the gate's headline, the instruction to accept the change, the command
    /// that does it, then the lines gone from the approved file and the lines new in the actual one.
    /// </summary>
    public static string BuildFailureMessage(string approved, string actual, string approvedPath, ApprovedSnapshotWording wording)
    {
        ArgumentNullException.ThrowIfNull(approved);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(wording);
        var receivedPath = ReceivedPathFor(approvedPath);

        var approvedLines = approved.Split('\n');
        var actualLines = actual.Split('\n');

        var removed = LinesMissingFrom(approvedLines, new HashSet<string>(actualLines, StringComparer.Ordinal), "  - ");
        var added = LinesMissingFrom(actualLines, new HashSet<string>(approvedLines, StringComparer.Ordinal), "  + ");

        var issue = wording.IssueReference is null ? string.Empty : " (" + wording.IssueReference + ")";

        return
            wording.Headline + "\n\n" +
            "This is not necessarily a bug -- it is the gate asking you to confirm the\n" +
            "change was intended" + issue + ". Read the lines below. If every one of them is a\n" +
            "change you meant to make, copy the received file over the approved file and\n" +
            "commit it IN THIS SAME PULL REQUEST so the change appears in the diff:\n\n" +
            $"  cp \"{receivedPath}\" \"{approvedPath}\"\n\n" +
            "Gone from the approved surface (" + wording.RemovedMeaning + "):\n" +
            (removed.Length == 0 ? "  (none)\n" : removed) +
            "\nNew in the actual surface:\n" +
            (added.Length == 0 ? "  (none)\n" : added);
    }

    /// <summary>Each non-empty line of <paramref name="lines"/> absent from <paramref name="other"/>, prefixed, in order.</summary>
    private static string LinesMissingFrom(string[] lines, HashSet<string> other, string prefix)
    {
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Length > 0 && !other.Contains(line))
            {
                builder.Append(prefix).Append(line).Append('\n');
            }
        }

        return builder.ToString();
    }
}
