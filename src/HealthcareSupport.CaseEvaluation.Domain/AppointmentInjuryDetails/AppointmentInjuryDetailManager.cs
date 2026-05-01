using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class AppointmentInjuryDetailManager : DomainService
{
    protected IAppointmentInjuryDetailRepository _appointmentInjuryDetailRepository;

    public AppointmentInjuryDetailManager(IAppointmentInjuryDetailRepository appointmentInjuryDetailRepository)
    {
        _appointmentInjuryDetailRepository = appointmentInjuryDetailRepository;
    }

    public virtual async Task<AppointmentInjuryDetail> CreateAsync(
        Guid appointmentId,
        DateTime dateOfInjury,
        string claimNumber,
        bool isCumulativeInjury,
        string bodyPartsSummary,
        DateTime? toDateOfInjury = null,
        string? wcabAdj = null,
        Guid? wcabOfficeId = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(claimNumber, nameof(claimNumber));
        Check.Length(claimNumber, nameof(claimNumber), AppointmentInjuryDetailConsts.ClaimNumberMaxLength);
        Check.NotNullOrWhiteSpace(bodyPartsSummary, nameof(bodyPartsSummary));
        Check.Length(bodyPartsSummary, nameof(bodyPartsSummary), AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength);
        Check.Length(wcabAdj, nameof(wcabAdj), AppointmentInjuryDetailConsts.WcabAdjMaxLength, 0);

        // OLD AppointmentInjuryDetailDomain.AddValidation: same (Appointment, ClaimNumber, DateOfInjury) tuple cannot repeat.
        var queryable = await _appointmentInjuryDetailRepository.GetQueryableAsync();
        var duplicate = queryable.Any(x =>
            x.AppointmentId == appointmentId &&
            x.ClaimNumber == claimNumber &&
            x.DateOfInjury == dateOfInjury);
        if (duplicate)
        {
            throw new UserFriendlyException("A claim with the same Claim Number and Date Of Injury already exists for this appointment.");
        }

        var entity = new AppointmentInjuryDetail(
            GuidGenerator.Create(),
            appointmentId,
            dateOfInjury,
            claimNumber,
            isCumulativeInjury,
            bodyPartsSummary,
            toDateOfInjury,
            wcabAdj,
            wcabOfficeId);
        return await _appointmentInjuryDetailRepository.InsertAsync(entity);
    }

    public virtual async Task<AppointmentInjuryDetail> UpdateAsync(
        Guid id,
        Guid appointmentId,
        DateTime dateOfInjury,
        string claimNumber,
        bool isCumulativeInjury,
        string bodyPartsSummary,
        DateTime? toDateOfInjury = null,
        string? wcabAdj = null,
        Guid? wcabOfficeId = null,
        [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(claimNumber, nameof(claimNumber));
        Check.Length(claimNumber, nameof(claimNumber), AppointmentInjuryDetailConsts.ClaimNumberMaxLength);
        Check.NotNullOrWhiteSpace(bodyPartsSummary, nameof(bodyPartsSummary));
        Check.Length(bodyPartsSummary, nameof(bodyPartsSummary), AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength);
        Check.Length(wcabAdj, nameof(wcabAdj), AppointmentInjuryDetailConsts.WcabAdjMaxLength, 0);

        var queryable = await _appointmentInjuryDetailRepository.GetQueryableAsync();
        var duplicate = queryable.Any(x =>
            x.Id != id &&
            x.AppointmentId == appointmentId &&
            x.ClaimNumber == claimNumber &&
            x.DateOfInjury == dateOfInjury);
        if (duplicate)
        {
            throw new UserFriendlyException("A claim with the same Claim Number and Date Of Injury already exists for this appointment.");
        }

        var entity = await _appointmentInjuryDetailRepository.GetAsync(id);
        entity.AppointmentId = appointmentId;
        entity.DateOfInjury = dateOfInjury;
        entity.ToDateOfInjury = toDateOfInjury;
        entity.ClaimNumber = claimNumber;
        entity.IsCumulativeInjury = isCumulativeInjury;
        entity.BodyPartsSummary = bodyPartsSummary;
        entity.WcabAdj = wcabAdj;
        entity.WcabOfficeId = wcabOfficeId;
        entity.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _appointmentInjuryDetailRepository.UpdateAsync(entity);
    }
}
