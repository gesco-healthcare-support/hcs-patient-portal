using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Patients;

public interface IPatientRepository : IRepository<Patient, Guid>
{
    Task<PatientWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PatientWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<Patient>> GetListAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the best-matching patient row (>=3 of FirstName / LastName / DateOfBirth /
    /// SSN / PhoneNumber / ZipCode equality matches) within the given tenant, or null if
    /// no row meets the threshold. Patient is NOT IMultiTenant -- the manual <c>tenantId</c>
    /// filter prevents cross-tenant PHI leak. Inputs are pre-normalised by the caller
    /// (lowercased / digit-stripped). Tie-break: higher match count, then oldest by
    /// CreationTime (first booked is canonical).
    /// </summary>
    Task<PatientMatchCandidate?> FindBestMatchAsync(
        Guid? tenantId,
        string firstName,
        string lastName,
        DateTime dateOfBirthDate,
        string? ssn,
        string? phone,
        string? zip,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 11k (2026-05-04) -- OLD-parity dedup-candidate prefilter.
    /// Mirrors OLD <c>AppointmentDomain.cs:736-738</c>'s SQL: select all
    /// patient rows in the calling tenant where ANY of the 6 dedup fields
    /// (LastName / DateOfBirth / PhoneNumber / Email / SocialSecurityNumber
    /// / linked-injury-ClaimNumber) matches the incoming intake. The
    /// caller (Application layer) then iterates and applies the
    /// <c>AppointmentBookingValidators.IsPatientDuplicate</c> 3-of-6
    /// threshold predicate to find the first true duplicate.
    ///
    /// Patient is NOT IMultiTenant -- this method MUST scope by
    /// <paramref name="tenantId"/> manually to prevent cross-tenant PHI
    /// leak (mirrors the existing <see cref="FindBestMatchAsync"/>
    /// approach).
    ///
    /// Distinct from <see cref="FindBestMatchAsync"/>: that method's
    /// field set diverged from OLD's (uses FirstName + ZipCode instead
    /// of Email + ClaimNumber). The two coexist; the OLD-parity flow
    /// (booking) uses this method, and any non-OLD callers can keep
    /// using <see cref="FindBestMatchAsync"/> until they migrate.
    /// </summary>
    /// <param name="tenantId">
    /// Calling tenant; null is host scope (rare, mostly for admin
    /// tooling). Required because <c>Patient</c> is not IMultiTenant.
    /// </param>
    /// <param name="lastName">Incoming intake's LastName.</param>
    /// <param name="dateOfBirth">Incoming intake's DateOfBirth.</param>
    /// <param name="phone">Incoming intake's PhoneNumber.</param>
    /// <param name="email">Incoming intake's Email.</param>
    /// <param name="ssn">Incoming intake's SocialSecurityNumber.</param>
    /// <param name="claimNumbers">
    /// Per-injury claim numbers from the booking intake. Per OLD line
    /// 738 the dedup matches when ANY existing patient row's ClaimNumber
    /// is in the incoming intake's set. Pass an empty collection if the
    /// intake has no injury rows yet (still does the other 5 fields).
    /// </param>
    /// <returns>
    /// All Patient rows matching at least one of the 6 fields. The
    /// caller applies the 3-of-6 threshold predicate to find the
    /// duplicate.
    /// </returns>
    Task<List<Patient>> GetDeduplicationCandidatesAsync(
        Guid? tenantId,
        string? lastName,
        DateTime? dateOfBirth,
        string? phone,
        string? email,
        string? ssn,
        IReadOnlyCollection<string?>? claimNumbers,
        CancellationToken cancellationToken = default);
}