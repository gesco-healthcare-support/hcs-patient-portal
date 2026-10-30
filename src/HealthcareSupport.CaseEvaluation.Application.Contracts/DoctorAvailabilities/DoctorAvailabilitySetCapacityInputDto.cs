using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-10-30 -- one capacity applied to one or more existing slots, all or nothing. Capacity 0
/// closes the slots to new requests; appointments already on them are untouched.
/// </summary>
public class DoctorAvailabilitySetCapacityInputDto
{
    /// <summary>
    /// The slots to change, each with the concurrency stamp the screen loaded it with. A slot
    /// someone else changed since then makes the whole request fail with a conflict.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(DoctorAvailabilityConsts.MaxSlotsPerCapacityChange)]
    public List<DoctorAvailabilitySlotStampDto> Slots { get; set; } = new();

    [Range(0, DoctorAvailabilityConsts.MaxCapacity)]
    public int Capacity { get; set; }

    /// <summary>
    /// When set, every slot must be at this location; the per-day change sends the location the
    /// schedule is showing, so it can never reach another location's slots on the same day.
    /// </summary>
    public Guid? LocationId { get; set; }
}

public class DoctorAvailabilitySlotStampDto
{
    public Guid Id { get; set; }

    /// <summary>Null skips the concurrency check for that slot.</summary>
    public string? ConcurrencyStamp { get; set; }
}

public class DoctorAvailabilitySetCapacityResultDto
{
    /// <summary>How many slots now have <see cref="Capacity"/>.</summary>
    public int UpdatedCount { get; set; }

    public int Capacity { get; set; }
}
