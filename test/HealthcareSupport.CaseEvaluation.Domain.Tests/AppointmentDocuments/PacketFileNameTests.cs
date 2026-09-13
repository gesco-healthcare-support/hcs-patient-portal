using System;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// #621 -- the packet file-name stamp must be PACIFIC, and the three call sites
/// must agree because they always had to.
///
/// <para><c>AppointmentPacket.GeneratedAt</c> is <c>DateTime.UtcNow</c>. The name
/// was formatted straight off it, so from 5pm Pacific onwards a packet carried
/// tomorrow's date -- on a document that reaches the patient, the doctor,
/// opposing counsel and the WCAB.</para>
/// </summary>
public class PacketFileNameTests
{
    // 02:00 UTC on 16 June is 19:00 Pacific on 15 June (PDT, UTC-7).
    private static readonly DateTime EveningPacific =
        new(2026, 6, 16, 2, 0, 0, DateTimeKind.Utc);

    // 18:00 UTC on 15 June is 11:00 Pacific the same day -- the two agree.
    private static readonly DateTime MiddayPacific =
        new(2026, 6, 15, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EveningPacketCarriesThePacificDate_NotTheUtcOne()
    {
        // THE REGRESSION. Formatting the raw instant gives 16062026.
        var name = PacketFileName.Build("A00042", PacketKind.Patient, EveningPacific);

        name.ShouldContain("15062026");
        name.ShouldNotContain("16062026");
    }

    [Fact]
    public void OutsideTheWindowTheStampIsUnchanged()
    {
        // The 16 hours a day when UTC and Pacific agree, which is why this survived
        // local verification for so long.
        PacketFileName.Build("A00042", PacketKind.Patient, MiddayPacific)
            .ShouldContain("15062026");
    }

    [Fact]
    public void TheWholeNameMatchesTheOldPattern()
    {
        // OLD parity: {Confirmation}_{Kind Label}_{ddMMyyyy_hhmmss}.pdf, spaces and
        // all. 19:00 Pacific formats as 07 under the 12-hour `hh` -- see below.
        PacketFileName.Build("A00042", PacketKind.Doctor, EveningPacific)
            .ShouldBe("A00042_Doctor Packet_15062026_070000.pdf");
    }

    [Fact]
    public void TwelveHourStampIsRetainedDeliberately_AndCollides()
    {
        // CHARACTERIZATION, not an endorsement. `hh` is 12-hour with no AM/PM, so
        // 07:00 and 19:00 Pacific produce the SAME name. That is inherited from OLD
        // and is flagged rather than silently corrected, because changing it is a
        // parity decision. This test exists so that if someone switches to `HH` it
        // fails and they have to make that decision on purpose.
        var morning = new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc); // 07:00 PDT
        var evening = new DateTime(2026, 6, 16, 2, 0, 0, DateTimeKind.Utc);  // 19:00 PDT

        PacketFileName.Build("A00042", PacketKind.Patient, morning)
            .ShouldBe(PacketFileName.Build("A00042", PacketKind.Patient, evening));
    }

    [Fact]
    public void ExtensionComesFromTheBlob()
    {
        PacketFileName.ExtensionFor("packets/x.docx").ShouldBe("docx");
        PacketFileName.ExtensionFor("packets/x.pdf").ShouldBe("pdf");
        PacketFileName.ExtensionFor("packets/x").ShouldBe("pdf");
        PacketFileName.ExtensionFor(null!).ShouldBe("pdf");
    }

    [Fact]
    public void TheCaseTrackerPayloadUsesTheSameNameAsTheDownload()
    {
        // The requirement DocumentEntryMapper states in prose -- "so a packet
        // downloaded from an email and the same packet referenced through the
        // integration carry the same name" -- asserted rather than remembered.
        // Before #621 this held only because three copies were kept in step by
        // hand, which is also how the UTC stamp reached all three.
        var direct = PacketFileName.Build("A00042", PacketKind.AttorneyClaimExaminer, EveningPacific);
        var viaIntegration = DocumentEntryMapper.PacketFileName(
            "A00042", PacketKind.AttorneyClaimExaminer, EveningPacific);

        viaIntegration.ShouldBe(direct);
    }

    [Fact]
    public void TheCaseTrackerLabelMatchesTheDownloadLabel()
    {
        foreach (PacketKind kind in Enum.GetValues<PacketKind>())
        {
            DocumentEntryMapper.PacketLabel(kind).ShouldBe(PacketFileName.Label(kind));
        }
    }
}
