using System.Text.Json;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.Json.SystemTextJson.JsonConverters;
using Volo.Abp.Json.SystemTextJson.Modifiers;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Timing;

/// <summary>
/// The wire half of <see cref="CalendarDateNormalizationTests"/>, for the two slot dates the
/// change-request queue sends.
///
/// <para>FOUND 2026-10-14 on the test server. Staff confirmed the Oct 20, 10:30 slot for a
/// reschedule; the approve modal showed "Oct 19, 2026 at 10:30" while the consent round, the
/// completed appointment and the consent email all said Oct 20. The entity column is exempt
/// from normalization, but a DTO property is a separate attribute target: ABP's
/// <c>AbpDateTimeConverterModifier</c> runs <c>IClock.Normalize</c> on every DTO
/// <c>DateTime</c> without <c>[DisableDateTimeNormalization]</c>, and with the clock pinned to
/// Utc that stamps the bare slot date as UTC midnight. A Pacific browser then renders it as the
/// evening before.</para>
///
/// <para>WHY THE OPTIONS ARE BUILT HERE AND NOT TAKEN FROM <c>IJsonSerializer</c>. ABP's own
/// serializer registers the converter with <c>SkipDateTimeNormalization()</c>, so it never adds a
/// Z and cannot show this defect. The HTTP API is configured separately, by ABP's
/// <c>MvcCoreBuilderExtensions.AddAbpJson</c>, WITHOUT the skip. The options below mirror that
/// method (Web defaults, the same resolver and modifier) so this asserts what a browser
/// receives.</para>
///
/// <para>The <c>CreationTime</c> assertion is the control. It carries the same Unspecified value
/// and no exemption, so it MUST gain a Z; if it does not, these options are not running the
/// converter and the two slot-date assertions would prove nothing.</para>
/// </summary>
public class ChangeRequestSlotDateSerializationTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    [Fact]
    public void SlotDates_GoOnTheWireWithoutAZone_WhileAnInstantKeepsItsZ()
    {
        var slotDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Unspecified);

        var serialized = JsonSerializer.Serialize(
            new AppointmentChangeRequestDto
            {
                RequestedSlotDate = slotDate,
                CurrentRoundProposedDate = slotDate,
                CreationTime = slotDate,
            },
            ApiJsonOptions());

        serialized.ShouldContain(
            "\"creationTime\":\"2026-10-20T00:00:00Z\"",
            Case.Sensitive,
            "Control: an unexempted DateTime must be normalized to UTC, or this test proves nothing.");
        serialized.ShouldContain(
            "\"requestedSlotDate\":\"2026-10-20T00:00:00\"",
            Case.Sensitive,
            "The requested slot date is a calendar date and must reach the client with no zone.");
        serialized.ShouldContain(
            "\"currentRoundProposedDate\":\"2026-10-20T00:00:00\"",
            Case.Sensitive,
            "The proposed reschedule date is a calendar date and must reach the client with no zone.");
    }

    /// <summary>The DateTime-relevant part of ABP's <c>AddAbpJson</c> (ABP 10.0).</summary>
    private JsonSerializerOptions ApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var resolver = new AbpDefaultJsonTypeInfoResolver(
            GetRequiredService<IOptions<AbpSystemTextJsonSerializerModifiersOptions>>());
        resolver.Modifiers.Add(
            new AbpDateTimeConverterModifier(
                    GetRequiredService<AbpDateTimeConverter>(),
                    GetRequiredService<AbpNullableDateTimeConverter>())
                .CreateModifyAction());
        options.TypeInfoResolver = resolver;
        return options;
    }
}
