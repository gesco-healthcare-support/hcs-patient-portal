using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.WcabOffices;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

[Audited]
public class AppointmentInjuryDetail : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    public DateTime DateOfInjury { get; set; }

    public DateTime? ToDateOfInjury { get; set; }

    [NotNull]
    public virtual string ClaimNumber { get; set; } = null!;

    public bool IsCumulativeInjury { get; set; }

    [CanBeNull]
    public virtual string? WcabAdj { get; set; }

    [NotNull]
    public virtual string BodyPartsSummary { get; set; } = null!;

    public Guid? WcabOfficeId { get; set; }

    protected AppointmentInjuryDetail()
    {
    }

    public AppointmentInjuryDetail(
        Guid id,
        Guid appointmentId,
        DateTime dateOfInjury,
        string claimNumber,
        bool isCumulativeInjury,
        string bodyPartsSummary,
        DateTime? toDateOfInjury = null,
        string? wcabAdj = null,
        Guid? wcabOfficeId = null)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(claimNumber, nameof(claimNumber));
        Check.Length(claimNumber, nameof(claimNumber), AppointmentInjuryDetailConsts.ClaimNumberMaxLength);
        Check.NotNullOrWhiteSpace(bodyPartsSummary, nameof(bodyPartsSummary));
        Check.Length(bodyPartsSummary, nameof(bodyPartsSummary), AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength);
        Check.Length(wcabAdj, nameof(wcabAdj), AppointmentInjuryDetailConsts.WcabAdjMaxLength, 0);
        AppointmentId = appointmentId;
        DateOfInjury = dateOfInjury;
        ToDateOfInjury = toDateOfInjury;
        ClaimNumber = claimNumber;
        IsCumulativeInjury = isCumulativeInjury;
        BodyPartsSummary = bodyPartsSummary;
        WcabAdj = wcabAdj;
        WcabOfficeId = wcabOfficeId;
    }
}
