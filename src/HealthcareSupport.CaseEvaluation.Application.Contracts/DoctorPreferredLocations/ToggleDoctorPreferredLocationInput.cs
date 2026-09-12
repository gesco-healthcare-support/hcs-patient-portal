using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

/// <summary>
/// Toggle a Location on or off for the supplied Doctor. Mirrors OLD's
/// <c>DoctorPreferredLocationDomain.Add/Update</c> upsert pattern at
/// <c>P:\PatientPortalOld\...\DoctorPreferredLocationDomain.cs</c>:45-108
/// where the AppService either inserts a new row or flips the existing
/// row's status, depending on whether the (DoctorId, LocationId) pair
/// already exists.
/// </summary>
public class ToggleDoctorPreferredLocationInput
{
    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    public bool IsActive { get; set; }
}
