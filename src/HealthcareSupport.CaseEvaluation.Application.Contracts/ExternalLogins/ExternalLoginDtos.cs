using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.ExternalLogins;

/// <summary>2026-11-13 -- the "Outside logins" list filter. Paged on the server.</summary>
public class GetExternalLoginsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Matches part of the name, surname, email or user name.</summary>
    public string? FilterText { get; set; }

    /// <summary>Null = every login; true = active only; false = switched off only.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>One outside user's login. Contact name and email only; nothing else about the person.</summary>
public class ExternalLoginDto
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    /// <summary>The outside role names the login holds (Patient, Applicant Attorney, ...).</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>The firm name given at sign-up, when there is one.</summary>
    public string? FirmName { get; set; }

    public bool IsActive { get; set; }

    public bool EmailConfirmed { get; set; }

    public DateTime CreationTime { get; set; }
}

/// <summary>Switch a login off (false) or back on (true).</summary>
public class ExternalLoginSetActiveDto
{
    public bool IsActive { get; set; }
}
