using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// One Send Back round for the staff review history (Branch 2). Rounds are returned
/// newest-first; <see cref="RoundNumber"/> is 1-based oldest-first. Counts and diffs
/// cover the flagged SCALAR fields only (documents excluded). Names are resolved
/// from the requester and the resubmitter (the row's last modifier when resolved).
/// </summary>
public class AppointmentInfoRequestRoundDto
{
    public Guid Id { get; set; }

    public int RoundNumber { get; set; }

    public string Note { get; set; } = string.Empty;

    public string? RequestedByName { get; set; }

    public DateTime RequestedAt { get; set; }

    public bool IsResolved { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResubmittedByName { get; set; }

    public int FlaggedCount { get; set; }

    public int FixedCount { get; set; }

    public List<InfoRequestFieldDiffDto> Diffs { get; set; } = new();
}
