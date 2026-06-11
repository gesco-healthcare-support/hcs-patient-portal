using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

[Audited]
public class AppointmentDefenseAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid DefenseAttorneyId { get; set; }

    public Guid? IdentityUserId { get; set; }

    // Paralegal delegate (2026-06-10, Phase 1): an optional paralegal who books /
    // manages the defense side on the attorney's behalf. Denormalized name+email
    // live here (there is no paralegal master entity, by design D2);
    // ParalegalIdentityUserId is backfilled when the invited paralegal registers
    // (ExternalSignupAppService.AutoLinkParalegalAsync), mirroring the attorney's
    // own IdentityUserId backfill. All nullable: most bookings carry no paralegal.
    public string? ParalegalEmail { get; set; }

    public string? ParalegalFirstName { get; set; }

    public string? ParalegalLastName { get; set; }

    public Guid? ParalegalIdentityUserId { get; set; }

    protected AppointmentDefenseAttorney()
    {
    }

    public AppointmentDefenseAttorney(Guid id, Guid appointmentId, Guid defenseAttorneyId, Guid? identityUserId)
    {
        Id = id;
        AppointmentId = appointmentId;
        DefenseAttorneyId = defenseAttorneyId;
        IdentityUserId = identityUserId;
    }
}
