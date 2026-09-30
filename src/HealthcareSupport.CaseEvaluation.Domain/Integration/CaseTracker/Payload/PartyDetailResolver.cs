using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Builds the attorney, insurance and claim-examiner sections.
///
/// <para>Attorneys are read from the appointment's own denormalised columns rather than the master
/// attorney list, so they reflect what was recorded for THIS appointment.</para>
///
/// <para>Insurances and claim examiners are appointment-level. The booking UI collects them through the
/// injury modal, so they LOOK per-injury, but neither entity stores an injury foreign key -- so they are
/// returned as flat collections and the receiver is told, in the contract, not to infer a link.</para>
///
/// <para>State names are resolved in ONE query for every referenced id across all of these, mirroring
/// <see cref="DocumentListResolver"/>'s category lookup, rather than a query per row.</para>
/// </summary>
public class PartyDetailResolver : ITransientDependency
{
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _insuranceRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<State, Guid> _stateRepository;

    public PartyDetailResolver(
        IRepository<AppointmentPrimaryInsurance, Guid> insuranceRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        IRepository<State, Guid> stateRepository)
    {
        _insuranceRepository = insuranceRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _stateRepository = stateRepository;
    }

    public virtual async Task<PartyDetailSection> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var insurances = await _insuranceRepository.GetListAsync(
            x => x.AppointmentId == appointment.Id && x.IsActive, cancellationToken: cancellationToken);
        var examiners = await _claimExaminerRepository.GetListAsync(
            x => x.AppointmentId == appointment.Id && x.IsActive, cancellationToken: cancellationToken);

        var stateNames = await ResolveStateNamesAsync(appointment, insurances, examiners, cancellationToken);

        return new PartyDetailSection
        {
            ApplicantAttorney = BuildApplicantAttorney(appointment, stateNames),
            DefenseAttorney = BuildDefenseAttorney(appointment, stateNames),
            PrimaryInsurances = insurances.Select(i => MapInsurance(i, stateNames)).ToList(),
            ClaimExaminers = examiners.Select(e => MapExaminer(e, stateNames)).ToList(),
        };
    }

    /// <summary>Every referenced state resolved in a single query, keyed by id.</summary>
    private async Task<Dictionary<Guid, string>> ResolveStateNamesAsync(
        Appointment appointment,
        List<AppointmentPrimaryInsurance> insurances,
        List<AppointmentClaimExaminer> examiners,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid?>
        {
            appointment.ApplicantAttorneyStateId,
            appointment.DefenseAttorneyStateId,
        };
        ids.AddRange(insurances.Select(i => i.StateId));
        ids.AddRange(examiners.Select(e => e.StateId));

        var wanted = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var states = await _stateRepository.GetListAsync(
            s => wanted.Contains(s.Id), cancellationToken: cancellationToken);

        return states.ToDictionary(s => s.Id, s => s.Name);
    }

    /// <summary>Null when the id is absent OR unresolvable, so a stale reference never throws.</summary>
    private static string? StateNameOrNull(Guid? stateId, Dictionary<Guid, string> stateNames) =>
        stateId is { } id && stateNames.TryGetValue(id, out var name) ? name : null;

    private static IntakeAttorneySection? BuildApplicantAttorney(
        Appointment appointment,
        Dictionary<Guid, string> stateNames)
    {
        var section = new IntakeAttorneySection
        {
            FirstName = appointment.ApplicantAttorneyFirstName,
            LastName = appointment.ApplicantAttorneyLastName,
            FirmName = appointment.ApplicantAttorneyFirmName,
            Email = appointment.ApplicantAttorneyEmail,
            PhoneNumber = appointment.ApplicantAttorneyPhoneNumber,
            FaxNumber = appointment.ApplicantAttorneyFaxNumber,
            WebAddress = appointment.ApplicantAttorneyWebAddress,
            Street = appointment.ApplicantAttorneyStreet,
            City = appointment.ApplicantAttorneyCity,
            State = StateNameOrNull(appointment.ApplicantAttorneyStateId, stateNames),
            ZipCode = appointment.ApplicantAttorneyZipCode,
        };

        return HasIdentity(section) ? section : null;
    }

    private static IntakeAttorneySection? BuildDefenseAttorney(
        Appointment appointment,
        Dictionary<Guid, string> stateNames)
    {
        var section = new IntakeAttorneySection
        {
            FirstName = appointment.DefenseAttorneyFirstName,
            LastName = appointment.DefenseAttorneyLastName,
            FirmName = appointment.DefenseAttorneyFirmName,
            Email = appointment.DefenseAttorneyEmail,
            PhoneNumber = appointment.DefenseAttorneyPhoneNumber,
            FaxNumber = appointment.DefenseAttorneyFaxNumber,
            WebAddress = appointment.DefenseAttorneyWebAddress,
            Street = appointment.DefenseAttorneyStreet,
            City = appointment.DefenseAttorneyCity,
            State = StateNameOrNull(appointment.DefenseAttorneyStateId, stateNames),
            ZipCode = appointment.DefenseAttorneyZipCode,
        };

        return HasIdentity(section) ? section : null;
    }

    /// <summary>
    /// An attorney with no name, firm or email is not a party -- it is empty columns. Return null so the
    /// receiver sees "none recorded" rather than an object full of nulls it has to interpret.
    /// </summary>
    private static bool HasIdentity(IntakeAttorneySection section) =>
        !string.IsNullOrWhiteSpace(section.FirstName)
        || !string.IsNullOrWhiteSpace(section.LastName)
        || !string.IsNullOrWhiteSpace(section.FirmName)
        || !string.IsNullOrWhiteSpace(section.Email);

    private static IntakeInsuranceSection MapInsurance(
        AppointmentPrimaryInsurance insurance,
        Dictionary<Guid, string> stateNames) =>
        new()
        {
            Name = insurance.Name,
            Suite = insurance.Suite,
            PhoneNumber = insurance.PhoneNumber,
            FaxNumber = insurance.FaxNumber,
            Street = insurance.Street,
            City = insurance.City,
            State = StateNameOrNull(insurance.StateId, stateNames),
            ZipCode = insurance.Zip,
        };

    private static IntakeClaimExaminerSection MapExaminer(
        AppointmentClaimExaminer examiner,
        Dictionary<Guid, string> stateNames) =>
        new()
        {
            Name = examiner.Name,
            Suite = examiner.Suite,
            Email = examiner.Email,
            PhoneNumber = examiner.PhoneNumber,
            FaxNumber = examiner.Fax,
            Street = examiner.Street,
            City = examiner.City,
            State = StateNameOrNull(examiner.StateId, stateNames),
            ZipCode = examiner.Zip,
        };
}

/// <summary>Result of <see cref="PartyDetailResolver"/>. Collections default to empty, attorneys to null.</summary>
public class PartyDetailSection
{
    public IntakeAttorneySection? ApplicantAttorney { get; set; }

    public IntakeAttorneySection? DefenseAttorney { get; set; }

    public List<IntakeInsuranceSection> PrimaryInsurances { get; set; } = new();

    public List<IntakeClaimExaminerSection> ClaimExaminers { get; set; } = new();
}
