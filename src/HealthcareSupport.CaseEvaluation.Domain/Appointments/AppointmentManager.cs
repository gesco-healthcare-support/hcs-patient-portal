using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class AppointmentManager : DomainService
{
    protected IAppointmentRepository _appointmentRepository;

    public AppointmentManager(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public virtual async Task<Appointment> CreateAsync(Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string requestConfirmationNumber, AppointmentStatusType appointmentStatus, string? panelNumber = null, DateTime? dueDate = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.NotNullOrWhiteSpace(requestConfirmationNumber, nameof(requestConfirmationNumber));
        Check.Length(requestConfirmationNumber, nameof(requestConfirmationNumber), AppointmentConsts.RequestConfirmationNumberMaxLength);
        Check.NotNull(appointmentStatus, nameof(appointmentStatus));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        var appointment = new Appointment(GuidGenerator.Create(), patientId, identityUserId, appointmentTypeId, locationId, doctorAvailabilityId, appointmentDate, requestConfirmationNumber, appointmentStatus, panelNumber, dueDate);
        return await _appointmentRepository.InsertAsync(appointment);
    }

    public virtual async Task<Appointment> UpdateAsync(Guid id, Guid patientId, Guid identityUserId, Guid appointmentTypeId, Guid locationId, Guid doctorAvailabilityId, DateTime appointmentDate, string? panelNumber = null, DateTime? dueDate = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(patientId, nameof(patientId));
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNull(locationId, nameof(locationId));
        Check.NotNull(doctorAvailabilityId, nameof(doctorAvailabilityId));
        Check.NotNull(appointmentDate, nameof(appointmentDate));
        Check.Length(panelNumber, nameof(panelNumber), AppointmentConsts.PanelNumberMaxLength);
        var appointment = await _appointmentRepository.GetAsync(id);
        appointment.PatientId = patientId;
        appointment.IdentityUserId = identityUserId;
        appointment.AppointmentTypeId = appointmentTypeId;
        appointment.LocationId = locationId;
        appointment.DoctorAvailabilityId = doctorAvailabilityId;
        appointment.AppointmentDate = appointmentDate;
        appointment.PanelNumber = panelNumber;
        appointment.DueDate = dueDate;
        appointment.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _appointmentRepository.UpdateAsync(appointment);
    }
}