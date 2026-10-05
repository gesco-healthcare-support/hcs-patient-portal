using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Issue #572 -- some notification codes seed the literal "Stub body for {code}" into every office
/// database. Measured 2026-10-03: 18 of the 70 codes in <c>Codes.All</c> (the issue's "23 of 64" is
/// stale), and NONE of the 18 is referenced by any dispatcher: no code path in <c>src/</c> names them,
/// so none can fire on its own. They are superseded or never-wired legacy codes
/// (e.g. AppointmentBooked -> AppointmentRequestedRegistered, AddInternalUser -> InternalUserCreated).
/// The one way a stub reaches an inbox is an IT admin's "Send test", which goes to their own address.
///
/// <para>This is a ratchet, in two halves. (1) The set of stub codes must equal
/// <see cref="DormantStubs"/>: a NEW code shipped without a body fails here, and so does a code that
/// gained a body but is still listed. (2) No dormant stub may be named as <c>Codes.X</c> anywhere in
/// <c>src/</c> outside the constants and subject map: the moment someone wires a sender to one, this
/// fails and the body must be written first.</para>
/// </summary>
public class StubTemplateRatchetTests
{
    // Not content to write: nothing sends these. Retiring them is a separate decision (they are
    // seeded into every office, editable by IT admin, and listed in the Angular template catalog).
    private static readonly string[] DormantStubs =
    {
        "AppointmentBooked", "AppointmentApproved", "AppointmentRejected",
        "RejectedPackageDocument", "RejectedJointDeclarationDocument",
        "AppointmentDueDate", "AppointmentDueDateUploadDocumentLeft", "SubmitQuery",
        "AppointmentApprovedStakeholderEmails", "AppointmentCancelledByAdmin",
        "AddInternalUser", "PatientDocumentAcceptedAttachment", "AppointmentPendingNextDay",
        "PatientAppointmentRescheduleReqAdmin", "PatientAppointmentRescheduleReqApproved",
        "PatientAppointmentRescheduleReqRejected", "PatientAppointmentCancellationApproved",
        "PatientAppointmentRescheduleReq",
    };

    private static readonly Regex CodeRef = new(@"Codes\.(\w+)\b", RegexOptions.Compiled);

    [Fact]
    public void The_stub_codes_are_exactly_the_known_dormant_ones()
    {
        var stubs = NotificationTemplateConsts.Codes.All
            .Where(c => !NotificationTemplateSeedDefaults.HasResourceBackedBody(c))
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        // CONTROL: the shipped content really does carry the stub text, or this reads nothing.
        stubs.ShouldNotBeEmpty();
        NotificationTemplateSeedDefaults.GetSeedDefaults(stubs[0]).BodyEmail.ShouldContain("Stub body for");

        stubs.ShouldBe(DormantStubs.OrderBy(c => c, StringComparer.Ordinal).ToArray(),
            "A notification code ships a stub body. Add an EmailBodies/<Code>.html (and a subject), "
            + "or, if nothing can send it, add it to DormantStubs with a reason.");
    }

    [Fact]
    public void No_dormant_stub_is_named_by_any_sender()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var constNames = typeof(NotificationTemplateConsts.Codes)
            .GetFields().Where(f => f.IsLiteral && DormantStubs.Contains((string)f.GetRawConstantValue()!))
            .Select(f => f.Name).ToHashSet();
        constNames.Count.ShouldBe(DormantStubs.Length);

        var seenAny = false;
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.Contains("/obj/") || rel.Contains("/bin/") || rel.Contains("/Migrations/") || rel.Contains("/TenantMigrations/")
                || rel.EndsWith("NotificationTemplateConsts.cs", StringComparison.Ordinal)
                || rel.EndsWith("EmailSubjects.cs", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)))
            {
                foreach (Match m in CodeRef.Matches(line))
                {
                    seenAny = true;
                    constNames.ShouldNotContain(m.Groups[1].Value,
                        $"{rel} names Codes.{m.Groups[1].Value}, which still seeds a stub body. Write its body before wiring a sender (#572).");
                }
            }
        }

        // CONTROL: the scan must see live references, or it is reading nothing.
        seenAny.ShouldBeTrue();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HealthcareSupport.CaseEvaluation.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("repository root (HealthcareSupport.CaseEvaluation.slnx) not found");
        return dir!.FullName;
    }
}
