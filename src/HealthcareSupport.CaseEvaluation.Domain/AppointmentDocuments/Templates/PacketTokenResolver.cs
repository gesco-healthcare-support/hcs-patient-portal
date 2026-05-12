using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using Microsoft.Extensions.Logging;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Default implementation. Loads an appointment + its full entity graph
/// across 14 repositories and produces a fully-populated
/// <see cref="PacketTokenContext"/>.
///
/// <para>OLD parity rules (from
/// <c>AppointmentDocumentDomain.cs:865-1190</c>):</para>
/// <list type="number">
///   <item>All single-row groups (Patients, Appointments, EmployerDetails,
///   PatientAttorneys, DefenseAttorneys, Others) take the FirstOrDefault
///   row. NEW null-guards instead of NREing on missing rows -- fixes OLD
///   bugs at <c>AppointmentDocumentDomain.cs:882, :886, :890</c>.</item>
///   <item>InjuryDetails group space-concatenates ALL injuries' values
///   with a trailing space per row (single-injury yields "VALUE ").
///   ToUpper applied. Per-injury joins (claim examiner address, primary
///   insurance address) take FirstOrDefault active row.</item>
///   <item>Every value uppercased at insertion, except none in the active
///   3-template universe (BodyPartDescription / CustomFieldValue tokens
///   only appear in unused templates).</item>
///   <item>Date format: MM/dd/yyyy. Time format: h:mm tt (12-hour en-US).
///   ParkingFee renders raw decimal without currency symbol or padding.</item>
///   <item>Names join IdentityUser.Name + " " + Surname (responsible user,
///   attorney users). When IdentityUserId is null, the corresponding
///   *Name token renders empty.</item>
///   <item>Signature bytes via <see cref="IUserSignatureAppService.GetBytesByUserIdAsync"/>;
///   null when no signature on file.</item>
/// </list>
/// </summary>
public class PacketTokenResolver : IPacketTokenResolver, ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerDetailRepository;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _appointmentApplicantAttorneyRepository;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _appointmentDefenseAttorneyRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _primaryInsuranceRepository;
    private readonly IRepository<WcabOffice, Guid> _wcabOfficeRepository;
    private readonly IRepository<State, Guid> _stateRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IBlobContainer<UserSignaturesContainer> _userSignaturesContainer;
    private readonly ILogger<PacketTokenResolver> _logger;

    public PacketTokenResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository,
        IRepository<Location, Guid> locationRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        IRepository<AppointmentEmployerDetail, Guid> employerDetailRepository,
        IRepository<AppointmentApplicantAttorney, Guid> appointmentApplicantAttorneyRepository,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository,
        IRepository<AppointmentDefenseAttorney, Guid> appointmentDefenseAttorneyRepository,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        IRepository<AppointmentPrimaryInsurance, Guid> primaryInsuranceRepository,
        IRepository<WcabOffice, Guid> wcabOfficeRepository,
        IRepository<State, Guid> stateRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IdentityUserManager userManager,
        IBlobContainer<UserSignaturesContainer> userSignaturesContainer,
        ILogger<PacketTokenResolver> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _locationRepository = locationRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _employerDetailRepository = employerDetailRepository;
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _injuryRepository = injuryRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _primaryInsuranceRepository = primaryInsuranceRepository;
        _wcabOfficeRepository = wcabOfficeRepository;
        _stateRepository = stateRepository;
        _identityUserRepository = identityUserRepository;
        _userManager = userManager;
        _userSignaturesContainer = userSignaturesContainer;
        _logger = logger;
    }

    public virtual async Task<PacketTokenContext> ResolveAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository.GetAsync(appointmentId, cancellationToken: cancellationToken);
        var ctx = new PacketTokenContext();

        await PopulatePatientAsync(ctx, appointment.PatientId, cancellationToken);
        await PopulateAppointmentAsync(ctx, appointment, cancellationToken);
        await PopulateEmployerAsync(ctx, appointmentId, cancellationToken);
        await PopulatePatientAttorneyAsync(ctx, appointmentId, cancellationToken);
        await PopulateDefenseAttorneyAsync(ctx, appointmentId, cancellationToken);
        await PopulateInjuryDetailsAsync(ctx, appointmentId, cancellationToken);

        ctx.DateNow = FormatDate(DateTime.Today);

        return ctx;
    }

    // -- Patient ----------------------------------------------------------

    private async Task PopulatePatientAsync(PacketTokenContext ctx, Guid patientId, CancellationToken ct)
    {
        var patient = await _patientRepository.FindAsync(patientId, cancellationToken: ct);
        if (patient == null)
        {
            return;
        }

        ctx.PatientFirstName = Upper(patient.FirstName);
        ctx.PatientLastName = Upper(patient.LastName);
        ctx.PatientMiddleName = Upper(patient.MiddleName);
        ctx.PatientDateOfBirth = FormatDate(patient.DateOfBirth);
        ctx.PatientSocialSecurityNumber = Upper(patient.SocialSecurityNumber);
        ctx.PatientStreet = Upper(patient.Street);
        ctx.PatientCity = Upper(patient.City);
        ctx.PatientZipCode = Upper(patient.ZipCode);
        ctx.PatientPhoneNumber = Upper(patient.PhoneNumber);
        ctx.PatientState = await ResolveStateNameAsync(patient.StateId, ct);
    }

    // -- Appointment + nested -----------------------------------------------

    private async Task PopulateAppointmentAsync(PacketTokenContext ctx, Appointment appointment, CancellationToken ct)
    {
        ctx.RequestConfirmationNumber = Upper(appointment.RequestConfirmationNumber);
        ctx.PanelNumber = Upper(appointment.PanelNumber);
        ctx.AppointmentCreatedDate = FormatDate(appointment.CreationTime);

        var availability = await _doctorAvailabilityRepository.FindAsync(appointment.DoctorAvailabilityId, cancellationToken: ct);
        if (availability != null)
        {
            ctx.AvailableDate = FormatDate(availability.AvailableDate);
            ctx.AppointmentTime = FormatTime(availability.FromTime);
        }

        var location = await _locationRepository.FindAsync(appointment.LocationId, cancellationToken: ct);
        if (location != null)
        {
            ctx.LocationName = Upper(location.Name);
            ctx.LocationAddress = Upper(location.Address);
            ctx.LocationCity = Upper(location.City);
            ctx.LocationZipCode = Upper(location.ZipCode);
            ctx.LocationParkingFee = FormatDecimal(location.ParkingFee);
            ctx.LocationState = await ResolveStateNameAsync(location.StateId, ct);
        }

        var appointmentType = await _appointmentTypeRepository.FindAsync(appointment.AppointmentTypeId, cancellationToken: ct);
        if (appointmentType != null)
        {
            ctx.AppointmentType = Upper(appointmentType.Name);
        }

        // Responsible user name + signature image bytes.
        if (appointment.PrimaryResponsibleUserId is { } responsibleUserId && responsibleUserId != Guid.Empty)
        {
            var responsibleUser = await _identityUserRepository.FindAsync(responsibleUserId, cancellationToken: ct);
            if (responsibleUser != null)
            {
                ctx.PrimaryResponsibleUserName = Upper(JoinNames(responsibleUser.Name, responsibleUser.Surname));
            }
            ctx.ResponsibleUserSignature = await GetSignatureBytesAsync(responsibleUserId);
        }
    }

    // -- EmployerDetails (FirstOrDefault) ----------------------------------

    private async Task PopulateEmployerAsync(PacketTokenContext ctx, Guid appointmentId, CancellationToken ct)
    {
        // GetQueryableAsync returns an IQueryable bound to the current UoW's
        // DbContext; the sync FirstOrDefault below blows up with
        // ObjectDisposedException once the surrounding job has no [UnitOfWork]
        // wrapper. The async repo terminator runs inside a UoW that the
        // extension method manages itself.
        var employer = await _employerDetailRepository.FirstOrDefaultAsync(
            x => x.AppointmentId == appointmentId, ct);
        if (employer == null)
        {
            return;
        }

        ctx.EmployerName = Upper(employer.EmployerName);
        ctx.EmployerStreet = Upper(employer.Street);
        ctx.EmployerCity = Upper(employer.City);
        ctx.EmployerZip = Upper(employer.ZipCode);
        ctx.EmployerState = await ResolveStateNameAsync(employer.StateId, ct);
    }

    // -- Patient Attorney (FirstOrDefault) ----------------------------------

    private async Task PopulatePatientAttorneyAsync(PacketTokenContext ctx, Guid appointmentId, CancellationToken ct)
    {
        var link = await _appointmentApplicantAttorneyRepository.FirstOrDefaultAsync(
            x => x.AppointmentId == appointmentId, ct);
        if (link == null)
        {
            return;
        }

        var attorney = await _applicantAttorneyRepository.FindAsync(link.ApplicantAttorneyId, cancellationToken: ct);
        if (attorney == null)
        {
            return;
        }

        ctx.PatientAttorneyStreet = Upper(attorney.Street);
        ctx.PatientAttorneyCity = Upper(attorney.City);
        ctx.PatientAttorneyZip = Upper(attorney.ZipCode);
        ctx.PatientAttorneyState = await ResolveStateNameAsync(attorney.StateId, ct);
        ctx.PatientAttorneyName = Upper(await ResolveAttorneyDisplayNameAsync(attorney.IdentityUserId, attorney.FirmName, ct));
    }

    // -- Defense Attorney (FirstOrDefault) ----------------------------------

    private async Task PopulateDefenseAttorneyAsync(PacketTokenContext ctx, Guid appointmentId, CancellationToken ct)
    {
        var link = await _appointmentDefenseAttorneyRepository.FirstOrDefaultAsync(
            x => x.AppointmentId == appointmentId, ct);
        if (link == null)
        {
            return;
        }

        var attorney = await _defenseAttorneyRepository.FindAsync(link.DefenseAttorneyId, cancellationToken: ct);
        if (attorney == null)
        {
            return;
        }

        ctx.DefenseAttorneyStreet = Upper(attorney.Street);
        ctx.DefenseAttorneyCity = Upper(attorney.City);
        ctx.DefenseAttorneyZip = Upper(attorney.ZipCode);
        ctx.DefenseAttorneyState = await ResolveStateNameAsync(attorney.StateId, ct);
        ctx.DefenseAttorneyName = Upper(await ResolveAttorneyDisplayNameAsync(attorney.IdentityUserId, attorney.FirmName, ct));
    }

    // -- Injury Details (multi-row, space-concat per OLD) -------------------

    private async Task PopulateInjuryDetailsAsync(PacketTokenContext ctx, Guid appointmentId, CancellationToken ct)
    {
        var injuries = await _injuryRepository.GetListAsync(
            x => x.AppointmentId == appointmentId, cancellationToken: ct);
        if (injuries.Count == 0)
        {
            return;
        }

        // Per-row collectors. We accumulate raw values per injury, then
        // join with the OLD-pattern trailing-space behavior.
        var claimNumbers = new List<string?>();
        var dateOfInjury = new List<string?>();
        var wcabAdj = new List<string?>();
        var wcabOfficeName = new List<string?>();
        var wcabOfficeAddress = new List<string?>();
        var wcabOfficeCity = new List<string?>();
        var wcabOfficeState = new List<string?>();
        var wcabOfficeZip = new List<string?>();
        var primaryInsuranceName = new List<string?>();
        var primaryInsuranceStreet = new List<string?>();
        var primaryInsuranceCity = new List<string?>();
        var primaryInsuranceState = new List<string?>();
        var primaryInsuranceZip = new List<string?>();
        var primaryInsurancePhone = new List<string?>();
        var claimExaminerName = new List<string?>();
        var claimExaminerStreet = new List<string?>();
        var claimExaminerCity = new List<string?>();
        var claimExaminerState = new List<string?>();
        var claimExaminerZip = new List<string?>();
        var claimExaminerPhone = new List<string?>();

        foreach (var injury in injuries)
        {
            claimNumbers.Add(injury.ClaimNumber);
            dateOfInjury.Add(FormatDate(injury.DateOfInjury));
            wcabAdj.Add(injury.WcabAdj);

            // WcabOffice via injury.WcabOfficeId
            WcabOffice? wcabOffice = null;
            if (injury.WcabOfficeId is { } wcabId)
            {
                wcabOffice = await _wcabOfficeRepository.FindAsync(wcabId, cancellationToken: ct);
            }
            wcabOfficeName.Add(wcabOffice?.Name);
            wcabOfficeAddress.Add(wcabOffice?.Address);
            wcabOfficeCity.Add(wcabOffice?.City);
            wcabOfficeZip.Add(wcabOffice?.ZipCode);
            wcabOfficeState.Add(await ResolveStateNameOrNullAsync(wcabOffice?.StateId, ct));

            // Primary insurance: first active per injury.
            var primaryInsurance = await _primaryInsuranceRepository.FirstOrDefaultAsync(
                x => x.AppointmentInjuryDetailId == injury.Id && x.IsActive, ct);
            primaryInsuranceName.Add(primaryInsurance?.Name);
            primaryInsuranceStreet.Add(primaryInsurance?.Street);
            primaryInsuranceCity.Add(primaryInsurance?.City);
            primaryInsuranceZip.Add(primaryInsurance?.Zip);
            primaryInsurancePhone.Add(primaryInsurance?.PhoneNumber);
            primaryInsuranceState.Add(await ResolveStateNameOrNullAsync(primaryInsurance?.StateId, ct));

            // Claim examiner: first active per injury.
            var claimExaminer = await _claimExaminerRepository.FirstOrDefaultAsync(
                x => x.AppointmentInjuryDetailId == injury.Id && x.IsActive, ct);
            claimExaminerName.Add(claimExaminer?.Name);
            claimExaminerStreet.Add(claimExaminer?.Street);
            claimExaminerCity.Add(claimExaminer?.City);
            claimExaminerZip.Add(claimExaminer?.Zip);
            claimExaminerPhone.Add(claimExaminer?.PhoneNumber);
            claimExaminerState.Add(await ResolveStateNameOrNullAsync(claimExaminer?.StateId, ct));
        }

        ctx.InjuryClaimNumber = ConcatPerInjury(claimNumbers);
        ctx.InjuryDateOfInjury = ConcatPerInjury(dateOfInjury);
        ctx.InjuryWcabAdj = ConcatPerInjury(wcabAdj);
        ctx.InjuryWcabOfficeName = ConcatPerInjury(wcabOfficeName);
        ctx.InjuryWcabOfficeAddress = ConcatPerInjury(wcabOfficeAddress);
        ctx.InjuryWcabOfficeCity = ConcatPerInjury(wcabOfficeCity);
        ctx.InjuryWcabOfficeState = ConcatPerInjury(wcabOfficeState);
        ctx.InjuryWcabOfficeZipCode = ConcatPerInjury(wcabOfficeZip);
        ctx.InjuryPrimaryInsuranceName = ConcatPerInjury(primaryInsuranceName);
        ctx.InjuryPrimaryInsuranceStreet = ConcatPerInjury(primaryInsuranceStreet);
        ctx.InjuryPrimaryInsuranceCity = ConcatPerInjury(primaryInsuranceCity);
        ctx.InjuryPrimaryInsuranceState = ConcatPerInjury(primaryInsuranceState);
        ctx.InjuryPrimaryInsuranceZip = ConcatPerInjury(primaryInsuranceZip);
        ctx.InjuryPrimaryInsurancePhoneNumber = ConcatPerInjury(primaryInsurancePhone);
        ctx.InjuryClaimExaminerName = ConcatPerInjury(claimExaminerName);
        ctx.InjuryClaimExaminerStreet = ConcatPerInjury(claimExaminerStreet);
        ctx.InjuryClaimExaminerCity = ConcatPerInjury(claimExaminerCity);
        ctx.InjuryClaimExaminerState = ConcatPerInjury(claimExaminerState);
        ctx.InjuryClaimExaminerZip = ConcatPerInjury(claimExaminerZip);
        ctx.InjuryClaimExaminerPhoneNumber = ConcatPerInjury(claimExaminerPhone);
    }

    // -- Helpers ------------------------------------------------------------

    /// <summary>
    /// Mirrors <c>UserSignatureAppService.GetBytesByUserIdAsync</c>. The
    /// AppService lives in Application.Contracts which Domain cannot
    /// reference, so the signature lookup is reproduced inline here. Both
    /// paths read the same blob keyed by the user's
    /// <c>UserSignatureBlobName</c> extra property.
    /// </summary>
    private async Task<byte[]?> GetSignatureBytesAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }
        var blobName = user.GetProperty<string>(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }
        return await _userSignaturesContainer.GetAllBytesOrNullAsync(blobName);
    }

    private async Task<string> ResolveStateNameAsync(Guid? stateId, CancellationToken ct)
    {
        return Upper(await ResolveStateNameOrNullAsync(stateId, ct));
    }

    private async Task<string?> ResolveStateNameOrNullAsync(Guid? stateId, CancellationToken ct)
    {
        if (stateId is not { } id || id == Guid.Empty)
        {
            return null;
        }
        var state = await _stateRepository.FindAsync(id, cancellationToken: ct);
        return state?.Name;
    }

    private async Task<string> ResolveAttorneyDisplayNameAsync(Guid? identityUserId, string? firmFallback, CancellationToken ct)
    {
        if (identityUserId is { } userId && userId != Guid.Empty)
        {
            var user = await _identityUserRepository.FindAsync(userId, cancellationToken: ct);
            if (user != null)
            {
                var combined = JoinNames(user.Name, user.Surname);
                if (!string.IsNullOrWhiteSpace(combined))
                {
                    return combined;
                }
            }
        }
        // Fall back to firm name when no IdentityUser is linked. OLD's
        // strict path would render empty here; firm name is the closest
        // human-readable substitute and avoids unsigned legal-document
        // sections in the rendered DOCX. Visual diff against OLD output
        // (Phase 1E.11) determines whether to keep or drop this fallback.
        return firmFallback ?? string.Empty;
    }

    private static string JoinNames(string? name, string? surname)
    {
        var first = name?.Trim() ?? string.Empty;
        var last = surname?.Trim() ?? string.Empty;
        if (first.Length == 0) return last;
        if (last.Length == 0) return first;
        return first + " " + last;
    }

    /// <summary>
    /// Uppercase + null-collapse to "". Mirrors OLD's
    /// <c>recordValue != null ? recordValue.ToString().ToUpper() : string.Empty</c>
    /// at <c>AppointmentDocumentDomain.cs:1070</c>.
    /// </summary>
    private static string Upper(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.ToUpper(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// MM/dd/yyyy then ToUpper (no-op for digits). Empty string when null.
    /// </summary>
    private static string FormatDate(DateTime? date)
    {
        if (!date.HasValue) return string.Empty;
        return date.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture).ToUpper(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// h:mm tt en-US then ToUpper. Empty string when null.
    /// </summary>
    private static string FormatTime(TimeOnly time)
    {
        return time.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"))
            .ToUpper(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Raw decimal.ToString() then ToUpper (no-op for digits). NO currency
    /// symbol, NO 2-decimal padding -- matches OLD's reflection-based
    /// stringification of the parking fee column.
    /// </summary>
    private static string FormatDecimal(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture).ToUpper(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Space-concatenate per-injury values. Mirrors OLD's pattern at
    /// <c>AppointmentDocumentDomain.cs:1139, :1160</c>:
    /// <c>recordValue += value + " "</c> across rows, then
    /// <c>.ToString().ToUpper()</c>. Single-injury appointments produce
    /// "VALUE " (trailing space preserved verbatim). Empty list -> "".
    /// </summary>
    private static string ConcatPerInjury(IEnumerable<string?> values)
    {
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            sb.Append(v ?? string.Empty).Append(' ');
        }
        if (sb.Length == 0) return string.Empty;
        return sb.ToString().ToUpper(CultureInfo.InvariantCulture);
    }
}
