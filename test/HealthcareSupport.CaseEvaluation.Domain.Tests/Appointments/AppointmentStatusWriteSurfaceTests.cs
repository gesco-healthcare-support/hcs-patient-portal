using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Issue #926 -- <c>Appointment.AppointmentStatus</c> has a public setter, so the transition guard in
/// <c>AppointmentManager.ApplyTransitionAsync</c> is opt-in. This pins WHO writes it, as a ratchet:
/// a new direct write anywhere in <c>src/</c> fails here by file name.
///
/// <para>The setter is now <c>internal</c> to the Domain assembly, so the compiler refuses a write from
/// Application; this scan stays as the ratchet on writes inside Domain. The allowlist is empty of
/// debt: only the state machine's own store accessor writes the status.</para>
/// </summary>
public class AppointmentStatusWriteSurfaceTests
{
    // member-access assignment only (x.AppointmentStatus = ...); DTO object initialisers are not writes
    // to the entity and are not matched. "==" is excluded.
    private static readonly Regex DirectWrite =
        new(@"\b\w+\.AppointmentStatus\s*=(?!=)", RegexOptions.Compiled);

    // path suffix -> exact number of direct writes permitted
    private static readonly Dictionary<string, int> Allowed = new()
    {
        // the state machine's own store accessor: THIS is the guarded write
        ["Domain/Appointments/AppointmentManager.cs"] = 1,
    };

    [Fact]
    public void Only_the_allowlisted_files_assign_AppointmentStatus_directly()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var found = new Dictionary<string, int>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.Contains("/Migrations/") || rel.Contains("/TenantMigrations/") || rel.Contains("/obj/") || rel.Contains("/bin/"))
            {
                continue;
            }

            var n = File.ReadAllLines(file).Count(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal) && DirectWrite.IsMatch(l));
            if (n > 0)
            {
                found[rel] = n;
            }
        }

        // CONTROL: the scan must see the machine's own write, or it is reading nothing.
        found.Keys.ShouldContain(k => k.EndsWith("Domain/Appointments/AppointmentManager.cs", StringComparison.Ordinal));

        foreach (var (rel, n) in found)
        {
            var key = Allowed.Keys.FirstOrDefault(a => rel.EndsWith(a, StringComparison.Ordinal));
            key.ShouldNotBeNull($"{rel} assigns AppointmentStatus directly ({n}x), bypassing the transition guard. Route it through AppointmentManager.ApplyTransitionAsync (#926).");
            n.ShouldBe(Allowed[key!], $"{rel}: direct AppointmentStatus writes changed from the allowlisted count. Fewer? lower the allowlist. More? use the manager.");
        }

        // a stale allowlist entry fails as loudly as a missing one
        foreach (var a in Allowed.Keys)
        {
            found.Keys.ShouldContain(k => k.EndsWith(a, StringComparison.Ordinal), $"allowlist entry {a} no longer writes the status; remove it");
        }
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
