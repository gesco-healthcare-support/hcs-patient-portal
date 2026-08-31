using System;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Timing;

/// <summary>
/// The one place that converts a stored UTC instant into the Pacific wall-clock value a human
/// reads. Added 2026-08-27.
///
/// <para>WHY THIS EXISTS. Timestamps are stored in UTC by decision, and the API container's clock
/// is UTC. Every surface that formatted a stored instant directly therefore rendered a UTC
/// wall-clock time, and every use of <c>DateTime.Today</c> asked the SERVER what day it is -- which
/// is tomorrow for the last 7-8 hours of every Pacific day. The business operates entirely in
/// Pacific time, so both are wrong, and wrong by a whole calendar day on records that are medical
/// and legal.</para>
///
/// <para>THE TRAP THIS API IS SHAPED TO PREVENT. Two kinds of value live in <c>DateTime</c> columns
/// here and they must be treated in OPPOSITE ways:</para>
/// <list type="bullet">
/// <item><description>An INSTANT -- a moment that happened: <c>CreationTime</c>,
/// <c>AppointmentApproveDate</c>, consent timestamps. Stored in UTC. Must be CONVERTED before it is
/// shown. Pass these to <see cref="FromUtc"/>.</description></item>
/// <item><description>A CALENDAR DATE -- a date someone wrote down: date of birth, date of injury,
/// the appointment date, an availability date. It has no time zone and no time of day. Converting
/// one shifts it 7-8 hours BACKWARD and lands it on the previous day. Do NOT pass these through
/// this class at all.</description></item>
/// </list>
///
/// <para>So there is deliberately no "format any DateTime" method: the caller has to say which kind
/// of value it holds, and the method names carry the answer. Nothing here reads the clock either --
/// the instant is always a parameter, so every call site takes its <c>now</c> from
/// <c>IClock</c> and a test can pin the instant and assert an exact string.</para>
///
/// <para>DST is handled by construction rather than by rule. Because the input is always a UTC
/// instant, <see cref="TimeZoneInfo.ConvertTimeFromUtc(DateTime, TimeZoneInfo)"/> picks the offset
/// that was actually in force at that instant. The November repeated hour (01:00-02:00 happens
/// twice) and the March skipped hour (02:00-03:00 does not exist) both come out right, which is the
/// reason UTC storage was kept: a local-only value inside the repeated hour cannot be ordered or
/// differenced at all.</para>
/// </summary>
public static class PacificTime
{
    /// <summary>IANA id. .NET 6+ resolves IANA ids on Windows too, via ICU.</summary>
    public const string IanaId = "America/Los_Angeles";

    /// <summary>Windows registry id, used only if the IANA lookup fails.</summary>
    private const string WindowsId = "Pacific Standard Time";

    /// <summary>
    /// America/Los_Angeles. Resolved once. THROWS if the platform cannot resolve it -- see
    /// <see cref="ResolveZone"/> for why there is no UTC fallback.
    /// </summary>
    public static TimeZoneInfo Zone { get; } = ResolveZone();

    /// <summary>
    /// Converts a stored UTC instant to the Pacific wall-clock value, kinded
    /// <see cref="DateTimeKind.Unspecified"/> because a wall-clock reading is neither UTC nor the
    /// reader's local time.
    ///
    /// <para>An <see cref="DateTimeKind.Unspecified"/> input is treated as UTC: that is how EF Core
    /// hands back a <c>datetime2</c> column, and storage is UTC by decision. A
    /// <see cref="DateTimeKind.Local"/> input is a bug at the call site and the BCL throws for it,
    /// which is the behaviour we want -- server-local time has no defined meaning here.</para>
    /// </summary>
    /// <param name="utcInstant">A moment that happened. NOT a calendar date -- see the class remarks.</param>
    public static DateTime FromUtc(DateTime utcInstant)
    {
        var utc = utcInstant.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc)
            : utcInstant;

        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
    }

    /// <summary>Nullable overload, so an absent instant stays absent instead of becoming a default date.</summary>
    public static DateTime? FromUtcOrNull(DateTime? utcInstant)
    {
        return utcInstant.HasValue ? FromUtc(utcInstant.Value) : null;
    }

    /// <summary>
    /// The Pacific calendar date at the given UTC instant, at midnight, kinded
    /// <see cref="DateTimeKind.Unspecified"/>.
    ///
    /// <para>This is the replacement for <c>DateTime.Today</c>, which on a UTC server answers a
    /// question nobody asked: what day it is in Greenwich. After 5pm Pacific those are different
    /// days, so <c>Today</c> silently rejected same-day appointments as past, shifted the booking
    /// horizon, and stamped generated packets with tomorrow's date.</para>
    /// </summary>
    public static DateTime TodayFrom(DateTime utcInstant)
    {
        return FromUtc(utcInstant).Date;
    }

    /// <summary>
    /// <c>PDT</c> or <c>PST</c> for the given instant -- whichever was actually in force. Use it
    /// wherever a bare time could be misread, which on a shared medical-legal record is most places.
    /// </summary>
    public static string Abbreviation(DateTime utcInstant)
    {
        var utc = utcInstant.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc)
            : utcInstant.ToUniversalTime();

        return Zone.IsDaylightSavingTime(utc) ? "PDT" : "PST";
    }

    /// <summary>
    /// <c>MM/dd/yyyy</c> in Pacific. Matches the date shape the packet templates and exports
    /// already use, so converting a surface changes the VALUE where it was wrong without changing
    /// the layout.
    /// </summary>
    public static string FormatDate(DateTime utcInstant)
    {
        return FromUtc(utcInstant).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// <c>MM/dd/yyyy h:mm tt PDT</c> in Pacific. The zone abbreviation is included because a date
    /// AND a time together is where a reader would otherwise have to guess the zone.
    /// </summary>
    public static string FormatDateTime(DateTime utcInstant)
    {
        var pacific = FromUtc(utcInstant);

        return string.Concat(
            pacific.ToString("MM/dd/yyyy h:mm tt", CultureInfo.GetCultureInfo("en-US")),
            " ",
            Abbreviation(utcInstant));
    }

    /// <summary>
    /// Resolves the zone, or throws.
    ///
    /// <para>There is deliberately NO fallback to <see cref="TimeZoneInfo.Utc"/>. A UTC fallback
    /// would make every surface render UTC while the code and the column names all claim Pacific --
    /// silently, correctly-looking, and off by 7-8 hours. That is the exact failure this class was
    /// written to remove, so an unresolvable zone must stop the app instead of degrading it.</para>
    ///
    /// <para>Verified 2026-08-27: <c>/usr/share/zoneinfo/America/Los_Angeles</c> is present in the
    /// deployed API container (Debian-based <c>mcr.microsoft.com/dotnet/aspnet:10.0</c>), and
    /// Windows dev machines resolve the IANA id through ICU. The Windows-id attempt covers a box
    /// running in globalization-invariant mode.</para>
    /// </summary>
    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaId);
        }
        catch (Exception ianaFailure) when (
            ianaFailure is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(WindowsId);
            }
            catch (Exception windowsFailure) when (
                windowsFailure is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new InvalidTimeZoneException(
                    $"Neither '{IanaId}' nor '{WindowsId}' could be resolved on this platform. " +
                    "The application renders every human-facing timestamp in Pacific time and " +
                    "cannot do that without the timezone database. On a Linux container, install " +
                    "the tzdata package.",
                    windowsFailure);
            }
        }
    }
}
