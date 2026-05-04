using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Patients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class EfCoreAppointmentRepository : EfCoreRepository<CaseEvaluationDbContext, Appointment, Guid>, IAppointmentRepository
{
    public EfCoreAppointmentRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<AppointmentWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var result = await (await GetDbSetAsync()).Where(b => b.Id == id).Select(appointment => new AppointmentWithNavigationProperties { Appointment = appointment, Patient = dbContext.Set<Patient>().FirstOrDefault(c => c.Id == appointment.PatientId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == appointment.IdentityUserId), AppointmentType = dbContext.Set<AppointmentType>().FirstOrDefault(c => c.Id == appointment.AppointmentTypeId), Location = dbContext.Set<Location>().FirstOrDefault(c => c.Id == appointment.LocationId), DoctorAvailability = dbContext.Set<DoctorAvailability>().FirstOrDefault(c => c.Id == appointment.DoctorAvailabilityId) }).FirstOrDefaultAsync(cancellationToken);

        if (result != null)
        {
            var appApplicantAttorney = await dbContext.Set<AppointmentApplicantAttorney>()
                .Where(aa => aa.AppointmentId == id)
                .FirstOrDefaultAsync(cancellationToken);
            if (appApplicantAttorney != null)
            {
                var applicantAttorney = await dbContext.Set<ApplicantAttorney>().FindAsync(new object[] { appApplicantAttorney.ApplicantAttorneyId }, cancellationToken);
                var applicantIdentityUser = await dbContext.Set<IdentityUser>().FindAsync(new object[] { appApplicantAttorney.IdentityUserId }, cancellationToken);
                if (applicantAttorney != null && applicantIdentityUser != null)
                {
                    result.AppointmentApplicantAttorney = new AppointmentApplicantAttorneyWithNavigationProperties
                    {
                        AppointmentApplicantAttorney = appApplicantAttorney,
                        Appointment = result.Appointment,
                        ApplicantAttorney = applicantAttorney,
                        IdentityUser = applicantIdentityUser,
                    };
                }
            }
        }

        return result!;
    }

    public virtual async Task<List<AppointmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, Guid? identityUserId = null, Guid? accessorIdentityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, AppointmentStatusType? appointmentStatus = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, IReadOnlyCollection<Guid>? visibleAppointmentIds = null, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(dbContext, query, filterText, panelNumber, appointmentDateMin, appointmentDateMax, identityUserId, accessorIdentityUserId, appointmentTypeId, locationId, appointmentStatus, visibleAppointmentIds);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<AppointmentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from appointment in (await GetDbSetAsync())
               join patient in (await GetDbContextAsync()).Set<Patient>() on appointment.PatientId equals patient.Id into patients
               from patient in patients.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on appointment.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               join appointmentType in (await GetDbContextAsync()).Set<AppointmentType>() on appointment.AppointmentTypeId equals appointmentType.Id into appointmentTypes
               from appointmentType in appointmentTypes.DefaultIfEmpty()
               join location in (await GetDbContextAsync()).Set<Location>() on appointment.LocationId equals location.Id into locations
               from location in locations.DefaultIfEmpty()
               join doctorAvailability in (await GetDbContextAsync()).Set<DoctorAvailability>() on appointment.DoctorAvailabilityId equals doctorAvailability.Id into doctorAvailabilities
               from doctorAvailability in doctorAvailabilities.DefaultIfEmpty()
               select new AppointmentWithNavigationProperties
               {
                   Appointment = appointment,
                   Patient = patient,
                   IdentityUser = identityUser,
                   AppointmentType = appointmentType,
                   Location = location,
                   DoctorAvailability = doctorAvailability
               };
    }

    protected virtual IQueryable<AppointmentWithNavigationProperties> ApplyFilter(CaseEvaluationDbContext dbContext, IQueryable<AppointmentWithNavigationProperties> query, string? filterText, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, Guid? identityUserId = null, Guid? accessorIdentityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, AppointmentStatusType? appointmentStatus = null, IReadOnlyCollection<Guid>? visibleAppointmentIds = null)
    {
        var accessorUserId = accessorIdentityUserId; // Capture for closure
        var ft = filterText;
        // S-NEW-2 (2026-04-30): when caller computed a visibility list (Patient,
        // AA, DA, CE involved-on-appointment), narrow the result. Empty list
        // means "user matches no appointment" -> return zero rows.
        if (visibleAppointmentIds != null)
        {
            var idsCopy = visibleAppointmentIds; // captured for closure
            query = query.Where(e => idsCopy.Contains(e.Appointment.Id));
        }
        return query
            // W1-4: extend FilterText to span PanelNumber + RequestConfirmationNumber
            // + Patient first/last name + booker IdentityUser name/surname so a single
            // free-text input on the office's queue page finds appointments by any
            // common identifier the office staff might recall.
            .WhereIf(!string.IsNullOrWhiteSpace(ft), e =>
                (e.Appointment.PanelNumber != null && e.Appointment.PanelNumber.Contains(ft!)) ||
                (e.Appointment.RequestConfirmationNumber != null && e.Appointment.RequestConfirmationNumber.Contains(ft!)) ||
                (e.Patient != null && e.Patient.FirstName != null && e.Patient.FirstName.Contains(ft!)) ||
                (e.Patient != null && e.Patient.LastName != null && e.Patient.LastName.Contains(ft!)) ||
                (e.IdentityUser != null && e.IdentityUser.Name != null && e.IdentityUser.Name.Contains(ft!)) ||
                (e.IdentityUser != null && e.IdentityUser.Surname != null && e.IdentityUser.Surname.Contains(ft!)))
            .WhereIf(!string.IsNullOrWhiteSpace(panelNumber), e => e.Appointment.PanelNumber!.Contains(panelNumber!))
            .WhereIf(appointmentDateMin.HasValue, e => e.Appointment.AppointmentDate >= appointmentDateMin!.Value)
            .WhereIf(appointmentDateMax.HasValue, e => e.Appointment.AppointmentDate <= appointmentDateMax!.Value)
            .WhereIf(identityUserId != null && identityUserId != Guid.Empty && (accessorIdentityUserId == null || accessorIdentityUserId == Guid.Empty), e =>
                (e.IdentityUser != null && e.IdentityUser.Id == identityUserId) ||
                (e.Patient != null && e.Patient.IdentityUserId == identityUserId))
            .WhereIf(accessorIdentityUserId != null && accessorIdentityUserId != Guid.Empty, e =>
                (e.Appointment.CreatorId != null && e.Appointment.CreatorId == accessorUserId) ||
                dbContext.Set<AppointmentAccessor>().Any(aa => aa.AppointmentId == e.Appointment.Id && aa.IdentityUserId == accessorUserId))
            .WhereIf(appointmentTypeId != null && appointmentTypeId != Guid.Empty, e => e.AppointmentType != null && e.AppointmentType.Id == appointmentTypeId)
            .WhereIf(locationId != null && locationId != Guid.Empty, e => e.Location != null && e.Location.Id == locationId)
            // W2-6: dashboard cards deep-link to /appointments?appointmentStatus=N.
            .WhereIf(appointmentStatus.HasValue, e => e.Appointment.AppointmentStatus == appointmentStatus!.Value);
    }

    protected virtual IQueryable<Appointment> ApplyFilter(IQueryable<Appointment> query, string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.PanelNumber!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(panelNumber), e => e.PanelNumber!.Contains(panelNumber!)).WhereIf(appointmentDateMin.HasValue, e => e.AppointmentDate >= appointmentDateMin!.Value).WhereIf(appointmentDateMax.HasValue, e => e.AppointmentDate <= appointmentDateMax!.Value);
    }

    public virtual async Task<List<Appointment>> GetListAsync(string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, panelNumber, appointmentDateMin, appointmentDateMax);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, Guid? identityUserId = null, Guid? accessorIdentityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, AppointmentStatusType? appointmentStatus = null, IReadOnlyCollection<Guid>? visibleAppointmentIds = null, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(dbContext, query, filterText, panelNumber, appointmentDateMin, appointmentDateMax, identityUserId, accessorIdentityUserId, appointmentTypeId, locationId, appointmentStatus, visibleAppointmentIds);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Appointment?> FindByConfirmationNumberAsync(
        string requestConfirmationNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestConfirmationNumber))
        {
            return null;
        }

        var dbSet = await GetDbSetAsync();
        // ABP's IMultiTenant data filter scopes this to the calling
        // tenant automatically. Order by CreationTime descending so a
        // future ReSubmit-of-a-ReSubmit chain returns the most recent
        // entry rather than the original.
        return await dbSet
            .Where(a => a.RequestConfirmationNumber == requestConfirmationNumber)
            .OrderByDescending(a => a.CreationTime)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }
}