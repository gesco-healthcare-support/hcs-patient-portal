using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

[Audited]
public class AppointmentApplicantAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid ApplicantAttorneyId { get; set; }

    public Guid? IdentityUserId { get; set; }

    // Paralegal delegate (2026-06-10, Phase 1): an optional paralegal who books /
    // manages the applicant side on the attorney's behalf. Denormalized name+email
    // live here (there is no paralegal master entity, by design D2);
    // ParalegalIdentityUserId is backfilled when the invited paralegal registers
    // (ExternalSignupAppService.AutoLinkParalegalAsync), mirroring the attorney's
    // own IdentityUserId backfill. All nullable: most bookings carry no paralegal.
    public string? ParalegalEmail { get; set; }

    public string? ParalegalFirstName { get; set; }

    public string? ParalegalLastName { get; set; }

    public Guid? ParalegalIdentityUserId { get; set; }

    protected AppointmentApplicantAttorney()
    {
    }

    public AppointmentApplicantAttorney(Guid id, Guid appointmentId, Guid applicantAttorneyId, Guid? identityUserId)
    {
        Id = id;
        AppointmentId = appointmentId;
        ApplicantAttorneyId = applicantAttorneyId;
        IdentityUserId = identityUserId;
    }
}