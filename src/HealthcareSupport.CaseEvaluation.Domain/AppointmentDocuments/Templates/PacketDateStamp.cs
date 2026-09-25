using System;
using System.Globalization;
using HealthcareSupport.CaseEvaluation.Timing;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// The date formatting behind the packet date tokens, as a seam that can be
/// tested (#710).
/// </summary>
/// <remarks>
/// <para>WHY THIS EXISTS AT ALL. <c>PacketTokenResolver</c> takes 22
/// constructor dependencies, one of which is a concrete
/// <c>IdentityUserManager</c> that NSubstitute cannot satisfy without
/// arguments. So the rule "the generated-on date is stamped in PACIFIC" was
/// asserted on the helper (<c>PacificTimeTests</c>) and on nothing at the
/// boundary that uses it -- the resolver's body is never executed by the suite,
/// because the one test that reaches it substitutes the interface.</para>
///
/// <para>The defect that rule prevents is not hypothetical. A packet generated
/// after about 5pm Pacific was stamped with TOMORROW's date, on a document that
/// is served as a legal record of an appointment. The fix is correct today and
/// nothing would have caught its removal.</para>
///
/// <para>THE TEST THIS MUST NOT ENABLE. With a real clock the only reachable
/// assertion is <c>DateNow == Format(PacificTime.TodayFrom(now))</c>, which
/// compares the code to itself and passes forever -- including with the Pacific
/// conversion deleted. The tests against this class pass a fixed evening
/// instant and assert a LITERAL expected string, so reverting the conversion
/// fails them.</para>
/// </remarks>
public static class PacketDateStamp
{
    /// <summary>
    /// <c>MM/dd/yyyy</c> then ToUpper (a no-op for digits, kept for OLD
    /// parity). Empty string when null, because a missing date renders as an
    /// empty token rather than the word "null" on a legal document.
    /// </summary>
    public static string Format(DateTime? date)
    {
        if (!date.HasValue)
        {
            return string.Empty;
        }

        return date.Value
            .ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
            .ToUpper(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The <c>##Others.DateNow##</c> token: the date the packet was generated,
    /// in PACIFIC.
    /// </summary>
    /// <param name="utcNow">
    /// The current instant from <c>IClock</c>, which is UTC because
    /// <c>AbpClockOptions.Kind</c> is pinned to Utc. Passed raw and converted
    /// here, so there is exactly one place the conversion can be forgotten.
    /// </param>
    public static string GeneratedOn(DateTime utcNow) =>
        Format(PacificTime.TodayFrom(utcNow));
}
