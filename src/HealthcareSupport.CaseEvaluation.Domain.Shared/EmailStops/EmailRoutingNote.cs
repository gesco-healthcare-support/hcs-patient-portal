namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- why an address on the staff "Who gets emails for this case" card does not get every case
/// email TODAY, before any stop (routing v2 + the paralegal rules). Purely descriptive; the card shows it
/// in plain words. Integer values travel on the wire; never renumber.
/// </summary>
public enum EmailRoutingNote
{
    /// <summary>Gets the case emails the routing rules send to their role.</summary>
    None = 0,

    /// <summary>The attorney sent all case notifications to their paralegal instead.</summary>
    SendsToParalegalInstead = 1,

    /// <summary>The booker opted out of following the case; they get the request receipt only.</summary>
    RequestReceiptOnly = 2,

    /// <summary>The patient on a request someone else booked; their emails start once it is approved.</summary>
    StartsAfterApproval = 3,
}
