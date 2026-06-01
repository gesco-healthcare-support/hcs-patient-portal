using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;
using Volo.Abp.Timing;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public class AppointmentInjuryDetailManager : DomainService
{
    protected IAppointmentInjuryDetailRepository _appointmentInjuryDetailRepository;
    protected IRepository<Appointment, Guid> _appointmentRepository;
    protected IRepository<Patient, Guid> _patientRepository;
    protected IClock _clock;

    public AppointmentInjuryDetailManager(
        IAppointmentInjuryDetailRepository appointmentInjuryDetailRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IClock clock)
    {
        _appointmentInjuryDetailRepository = appointmentInjuryDetailRepository;
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _clock = clock;
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

        await ValidateInjuryDatesAsync(appointmentId, dateOfInjury, isCumulativeInjury, toDateOfInjury);

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

        await ValidateInjuryDatesAsync(appointmentId, dateOfInjury, isCumulativeInjury, toDateOfInjury);

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

    /// <summary>
    /// OLD parity (AppointmentDomain.CommonValidation +
    /// AppointmentInjuryDetailDomain.CommonValidation): an injury date may not be
    /// in the future nor precede the patient's date of birth, and a cumulative-trauma
    /// range must run From &lt; To, end on or before today, and span more than a
    /// single day. Enforced in the manager so both the booking-time cascade and the
    /// standalone add/edit endpoint are covered.
    /// </summary>
    protected virtual async Task ValidateInjuryDatesAsync(
        Guid appointmentId,
        DateTime dateOfInjury,
        bool isCumulativeInjury,
        DateTime? toDateOfInjury)
    {
        var today = _clock.Now.Date;

        if (dateOfInjury.Date > today)
        {
            throw new UserFriendlyException("Injury date cannot be in the future.");
        }

        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        var patient = await _patientRepository.GetAsync(appointment.PatientId);
        if (patient.DateOfBirth.Date > dateOfInjury.Date)
        {
            throw new UserFriendlyException("Injury date cannot be earlier than the patient's date of birth.");
        }

        if (isCumulativeInjury && toDateOfInjury.HasValue)
        {
            var toDate = toDateOfInjury.Value.Date;
            if (dateOfInjury.Date > toDate)
            {
                throw new UserFriendlyException("Injury 'From' date must be earlier than the 'To' date.");
            }
            if (toDate > today)
            {
                throw new UserFriendlyException("Injury 'To' date cannot be in the future.");
            }
            if (dateOfInjury.Date == toDate)
            {
                throw new UserFriendlyException("Injury 'From' and 'To' dates must be different.");
            }
        }
    }
}
