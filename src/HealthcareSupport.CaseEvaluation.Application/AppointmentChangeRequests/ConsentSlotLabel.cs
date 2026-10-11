using System.Globalization;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// "Oct 21, 2026 at 9:00 AM" for a proposed slot -- the wording both consent surfaces (the
/// emailed link's landing page and the in-portal card) show a party, kept in one place so the
/// two cannot drift. The slot's date is date-only and its start time is a wall-clock
/// <see cref="System.TimeOnly"/>; neither is converted between time zones.
/// </summary>
public static class ConsentSlotLabel
{
    private static readonly CultureInfo UsEnglish = CultureInfo.GetCultureInfo("en-US");

    public static string Format(DoctorAvailability slot)
    {
        var date = slot.AvailableDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        var time = slot.FromTime.ToString("h:mm tt", UsEnglish);
        return $"{date} at {time}";
    }
}
