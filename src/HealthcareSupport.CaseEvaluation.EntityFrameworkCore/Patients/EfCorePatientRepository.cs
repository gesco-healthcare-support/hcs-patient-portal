using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Saas.Tenants;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.States;
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

namespace HealthcareSupport.CaseEvaluation.Patients;

public class EfCorePatientRepository : EfCoreRepository<CaseEvaluationDbContext, Patient, Guid>, IPatientRepository
{
    public EfCorePatientRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<PatientWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await (await GetDbSetAsync()).Where(b => b.Id == id).Select(patient => new PatientWithNavigationProperties { Patient = patient, State = dbContext.Set<State>().FirstOrDefault(c => c.Id == patient.StateId), AppointmentLanguage = dbContext.Set<AppointmentLanguage>().FirstOrDefault(c => c.Id == patient.AppointmentLanguageId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == patient.IdentityUserId), Tenant = dbContext.Set<Tenant>().FirstOrDefault(c => c.Id == patient.TenantId) }).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<PatientWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firstName, lastName, middleName, email, genderId, dateOfBirthMin, dateOfBirthMax, phoneNumber, socialSecurityNumber, address, city, zipCode, refferedBy, cellPhoneNumber, street, interpreterVendorName, apptNumber, stateId, appointmentLanguageId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? PatientConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<PatientWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from patient in (await GetDbSetAsync())
               join state in (await GetDbContextAsync()).Set<State>() on patient.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               join appointmentLanguage in (await GetDbContextAsync()).Set<AppointmentLanguage>() on patient.AppointmentLanguageId equals appointmentLanguage.Id into appointmentLanguages
               from appointmentLanguage in appointmentLanguages.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on patient.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               join tenant in (await GetDbContextAsync()).Set<Tenant>() on patient.TenantId equals tenant.Id into tenants
               from tenant in tenants.DefaultIfEmpty()
               select new PatientWithNavigationProperties
               {
                   Patient = patient,
                   State = state,
                   AppointmentLanguage = appointmentLanguage,
                   IdentityUser = identityUser,
                   Tenant = tenant
               };
    }

    protected virtual IQueryable<PatientWithNavigationProperties> ApplyFilter(IQueryable<PatientWithNavigationProperties> query, string? filterText, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Patient.FirstName!.Contains(filterText!) || e.Patient.LastName!.Contains(filterText!) || e.Patient.MiddleName!.Contains(filterText!) || e.Patient.Email!.Contains(filterText!) || e.Patient.PhoneNumber!.Contains(filterText!) || e.Patient.SocialSecurityNumber!.Contains(filterText!) || e.Patient.Address!.Contains(filterText!) || e.Patient.City!.Contains(filterText!) || e.Patient.ZipCode!.Contains(filterText!) || e.Patient.RefferedBy!.Contains(filterText!) || e.Patient.CellPhoneNumber!.Contains(filterText!) || e.Patient.Street!.Contains(filterText!) || e.Patient.InterpreterVendorName!.Contains(filterText!) || e.Patient.ApptNumber!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firstName), e => e.Patient.FirstName!.Contains(firstName!)).WhereIf(!string.IsNullOrWhiteSpace(lastName), e => e.Patient.LastName!.Contains(lastName!)).WhereIf(!string.IsNullOrWhiteSpace(middleName), e => e.Patient.MiddleName!.Contains(middleName!)).WhereIf(!string.IsNullOrWhiteSpace(email), e => e.Patient.Email!.Contains(email!)).WhereIf(genderId.HasValue, e => e.Patient.GenderId == genderId).WhereIf(dateOfBirthMin.HasValue, e => e.Patient.DateOfBirth >= dateOfBirthMin!.Value).WhereIf(dateOfBirthMax.HasValue, e => e.Patient.DateOfBirth <= dateOfBirthMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.Patient.PhoneNumber!.Contains(phoneNumber!)).WhereIf(!string.IsNullOrWhiteSpace(socialSecurityNumber), e => e.Patient.SocialSecurityNumber!.Contains(socialSecurityNumber!)).WhereIf(!string.IsNullOrWhiteSpace(address), e => e.Patient.Address!.Contains(address!)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.Patient.City!.Contains(city!)).WhereIf(!string.IsNullOrWhiteSpace(zipCode), e => e.Patient.ZipCode!.Contains(zipCode!)).WhereIf(!string.IsNullOrWhiteSpace(refferedBy), e => e.Patient.RefferedBy!.Contains(refferedBy!)).WhereIf(!string.IsNullOrWhiteSpace(cellPhoneNumber), e => e.Patient.CellPhoneNumber!.Contains(cellPhoneNumber!)).WhereIf(!string.IsNullOrWhiteSpace(street), e => e.Patient.Street!.Contains(street!)).WhereIf(!string.IsNullOrWhiteSpace(interpreterVendorName), e => e.Patient.InterpreterVendorName!.Contains(interpreterVendorName!)).WhereIf(!string.IsNullOrWhiteSpace(apptNumber), e => e.Patient.ApptNumber!.Contains(apptNumber!)).WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId).WhereIf(appointmentLanguageId != null && appointmentLanguageId != Guid.Empty, e => e.AppointmentLanguage != null && e.AppointmentLanguage.Id == appointmentLanguageId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    protected virtual IQueryable<Patient> ApplyFilter(IQueryable<Patient> query, string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.FirstName!.Contains(filterText!) || e.LastName!.Contains(filterText!) || e.MiddleName!.Contains(filterText!) || e.Email!.Contains(filterText!) || e.PhoneNumber!.Contains(filterText!) || e.SocialSecurityNumber!.Contains(filterText!) || e.Address!.Contains(filterText!) || e.City!.Contains(filterText!) || e.ZipCode!.Contains(filterText!) || e.RefferedBy!.Contains(filterText!) || e.CellPhoneNumber!.Contains(filterText!) || e.Street!.Contains(filterText!) || e.InterpreterVendorName!.Contains(filterText!) || e.ApptNumber!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firstName), e => e.FirstName!.Contains(firstName!)).WhereIf(!string.IsNullOrWhiteSpace(lastName), e => e.LastName!.Contains(lastName!)).WhereIf(!string.IsNullOrWhiteSpace(middleName), e => e.MiddleName!.Contains(middleName!)).WhereIf(!string.IsNullOrWhiteSpace(email), e => e.Email!.Contains(email!)).WhereIf(genderId.HasValue, e => e.GenderId == genderId).WhereIf(dateOfBirthMin.HasValue, e => e.DateOfBirth >= dateOfBirthMin!.Value).WhereIf(dateOfBirthMax.HasValue, e => e.DateOfBirth <= dateOfBirthMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.PhoneNumber!.Contains(phoneNumber!)).WhereIf(!string.IsNullOrWhiteSpace(socialSecurityNumber), e => e.SocialSecurityNumber!.Contains(socialSecurityNumber!)).WhereIf(!string.IsNullOrWhiteSpace(address), e => e.Address!.Contains(address!)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City!.Contains(city!)).WhereIf(!string.IsNullOrWhiteSpace(zipCode), e => e.ZipCode!.Contains(zipCode!)).WhereIf(!string.IsNullOrWhiteSpace(refferedBy), e => e.RefferedBy!.Contains(refferedBy!)).WhereIf(!string.IsNullOrWhiteSpace(cellPhoneNumber), e => e.CellPhoneNumber!.Contains(cellPhoneNumber!)).WhereIf(!string.IsNullOrWhiteSpace(street), e => e.Street!.Contains(street!)).WhereIf(!string.IsNullOrWhiteSpace(interpreterVendorName), e => e.InterpreterVendorName!.Contains(interpreterVendorName!)).WhereIf(!string.IsNullOrWhiteSpace(apptNumber), e => e.ApptNumber!.Contains(apptNumber!));
    }

    public virtual async Task<List<Patient>> GetListAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, firstName, lastName, middleName, email, genderId, dateOfBirthMin, dateOfBirthMax, phoneNumber, socialSecurityNumber, address, city, zipCode, refferedBy, cellPhoneNumber, street, interpreterVendorName, apptNumber);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? PatientConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firstName, lastName, middleName, email, genderId, dateOfBirthMin, dateOfBirthMax, phoneNumber, socialSecurityNumber, address, city, zipCode, refferedBy, cellPhoneNumber, street, interpreterVendorName, apptNumber, stateId, appointmentLanguageId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<PatientMatchCandidate?> FindBestMatchAsync(
        Guid? tenantId,
        string firstName,
        string lastName,
        DateTime dateOfBirthDate,
        string? ssn,
        string? phone,
        string? zip,
        CancellationToken cancellationToken = default)
    {
        // Patient is NOT IMultiTenant; manual TenantId filter is mandatory to avoid
        // cross-tenant PHI leak (see EntityFrameworkCore/CLAUDE.md "Multi-tenancy").
        // OLD reference: AppointmentDomain.IsPatientRegistered (3-of-6 LINQ match).
        var dob = dateOfBirthDate.Date;
        var fn = firstName;
        var ln = lastName;

        var query = (await GetQueryableAsync())
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.CreationTime,
                MatchCount =
                    (x.FirstName.ToLower() == fn ? 1 : 0) +
                    (x.LastName.ToLower()  == ln ? 1 : 0) +
                    (x.DateOfBirth == dob               ? 1 : 0) +
                    (ssn   != null && x.SocialSecurityNumber == ssn   ? 1 : 0) +
                    (phone != null && x.PhoneNumber          == phone ? 1 : 0) +
                    (zip   != null && x.ZipCode              == zip   ? 1 : 0)
            });

        var best = await query
            .Where(c => c.MatchCount >= PatientMatching.MinMatchCount)
            .OrderByDescending(c => c.MatchCount)
            .ThenBy(c => c.CreationTime)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        return best is null
            ? null
            : new PatientMatchCandidate(best.Id, best.MatchCount, best.CreationTime);
    }
}