using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>2026-11-05 -- the staff "Who gets emails for this case" card.</summary>
public class AppointmentEmailRecipientsDto
{
    /// <summary>External addresses only (never the office inbox or staff).</summary>
    public List<AppointmentEmailRecipientDto> Items { get; set; } = new();
}

/// <summary>One external person the case emails, with who they are and whether their emails are stopped.</summary>
public class AppointmentEmailRecipientDto
{
    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }

    public List<EmailRecipientKind> Kinds { get; set; } = new();

    public EmailRoutingNote RoutingNote { get; set; }

    /// <summary>Set when this address is stopped for this case.</summary>
    public EmailStopStatusDto? CaseStop { get; set; }

    /// <summary>Set when this address is stopped for all cases (the host list).</summary>
    public EmailStopStatusDto? AllCasesStop { get; set; }
}

/// <summary>One active email stop, as shown on the card.</summary>
public class EmailStopStatusDto
{
    public Guid Id { get; set; }

    public EmailStopScope Scope { get; set; }

    public string? StoppedByName { get; set; }

    public DateTime StoppedAt { get; set; }

    public string? Note { get; set; }

    /// <summary>All-cases stops only: the practice it was stopped from (null when stopped on the staff site).</summary>
    public string? StoppedFromOfficeName { get; set; }
}

/// <summary>"Stop emails" from the case card.</summary>
public class StopAppointmentEmailsInput
{
    [Required]
    [StringLength(EmailStopConsts.EmailMaxLength)]
    public string Email { get; set; } = string.Empty;

    public EmailStopScope Scope { get; set; } = EmailStopScope.Case;

    [StringLength(EmailStopConsts.NoteMaxLength)]
    public string? Note { get; set; }
}

/// <summary>
/// The warning shown before a stop is saved: whether the address is the ONLY one able to answer Agree/Decline
/// for a side. A warning, never a block.
/// </summary>
public class EmailStopConsentImpactDto
{
    public bool HasImpact { get; set; }

    /// <summary>Sides on THIS case where the address is the only one able to answer.</summary>
    public List<ChangeRequestSide> ThisCaseSides { get; set; } = new();

    /// <summary>All-cases stops only: other cases in this practice with an open request this address alone can answer.</summary>
    public List<EmailStopConsentImpactCaseDto> OtherCases { get; set; } = new();
}

public class EmailStopConsentImpactCaseDto
{
    public Guid AppointmentId { get; set; }

    public string RequestConfirmationNumber { get; set; } = string.Empty;

    public ChangeRequestSide Side { get; set; }
}

/// <summary>The "Stopped emails" page: the all-cases list.</summary>
public class GetGlobalEmailStopsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Matches part of the email address.</summary>
    public string? Filter { get; set; }
}

public class GlobalEmailStopDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? StoppedByName { get; set; }

    public DateTime StoppedAt { get; set; }

    public string? Note { get; set; }

    public string? StoppedFromOfficeName { get; set; }
}

/// <summary>"Stop emails to an address" on the "Stopped emails" page (no case needed).</summary>
public class CreateGlobalEmailStopInput
{
    [Required]
    [StringLength(EmailStopConsts.EmailMaxLength)]
    public string Email { get; set; } = string.Empty;

    [StringLength(EmailStopConsts.NoteMaxLength)]
    public string? Note { get; set; }
}
