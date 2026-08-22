using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Writes a booking's child groups by calling the EXISTING child app services, and reports how many
/// rows each group produced.
///
/// <para><b>Why it delegates instead of writing rows itself.</b> Each child app service already
/// owns that group's validation and mapping. Re-expressing those writes against repositories here
/// would create a second implementation that can drift from the first -- which is Bug F18 exactly,
/// a cascade that silently dropped 2 of 8 groups. Delegating keeps one implementation per group.</para>
///
/// <para><b>Why this is still atomic.</b> None of the child app services opens its own unit of work
/// or forces a save, so they all enlist in the caller's ambient unit of work. The caller marks the
/// submit <c>[UnitOfWork]</c>; one commit covers every group, and any throw rolls all of them back.
/// Verified on main 2026-08-21: no <c>RequiresNew</c>, no <c>isTransactional</c>, no
/// <c>SaveChangesAsync</c> and no <c>autoSave: true</c> in any of the six areas.</para>
/// </summary>
public class AppointmentChildGroupWriter : ITransientDependency
{
    private readonly IAppointmentEmployerDetailsAppService _employerDetails;
    private readonly IAppointmentPrimaryInsurancesAppService _primaryInsurances;
    private readonly IAppointmentClaimExaminersAppService _claimExaminers;
    private readonly IAppointmentInjuryDetailsAppService _injuryDetails;
    private readonly IAppointmentBodyPartsAppService _bodyParts;
    private readonly IAppointmentAccessorsAppService _accessors;

    public AppointmentChildGroupWriter(
        IAppointmentEmployerDetailsAppService employerDetails,
        IAppointmentPrimaryInsurancesAppService primaryInsurances,
        IAppointmentClaimExaminersAppService claimExaminers,
        IAppointmentInjuryDetailsAppService injuryDetails,
        IAppointmentBodyPartsAppService bodyParts,
        IAppointmentAccessorsAppService accessors)
    {
        _employerDetails = employerDetails;
        _primaryInsurances = primaryInsurances;
        _claimExaminers = claimExaminers;
        _injuryDetails = injuryDetails;
        _bodyParts = bodyParts;
        _accessors = accessors;
    }

    /// <summary>
    /// Writes every child group present in <paramref name="input"/> against
    /// <paramref name="appointmentId"/>, recording per-group counts on <paramref name="result"/>.
    /// Each group's own <c>AppointmentId</c> is overwritten here: the caller could not have known
    /// it when the request was built.
    /// </summary>
    public virtual async Task WriteAllAsync(
        Guid appointmentId,
        AppointmentSubmitDto input,
        AppointmentSubmitResultDto result)
    {
        result.EmployerDetails = await WriteSingleAsync(
            input.EmployerDetail,
            dto => dto.AppointmentId = appointmentId,
            dto => _employerDetails.CreateAsync(dto));

        result.PrimaryInsurances = await WriteSingleAsync(
            input.PrimaryInsurance,
            dto => dto.AppointmentId = appointmentId,
            dto => _primaryInsurances.CreateAsync(dto));

        result.ClaimExaminers = await WriteSingleAsync(
            input.ClaimExaminer,
            dto => dto.AppointmentId = appointmentId,
            dto => _claimExaminers.CreateAsync(dto));

        result.Accessors = await WriteManyAsync(
            input.Accessors,
            dto => dto.AppointmentId = appointmentId,
            dto => _accessors.CreateAsync(dto));

        // Injuries before their body parts, and one at a time: a body part's parent is the injury
        // (AppointmentBodyPartCreateDto.AppointmentInjuryDetailId), not the appointment, so the
        // injury's id has to exist before its parts can be written.
        foreach (var injury in input.InjuryDetails)
        {
            injury.Injury.AppointmentId = appointmentId;
            var createdInjury = await _injuryDetails.CreateAsync(injury.Injury);
            result.InjuryDetails++;

            result.BodyParts += await WriteManyAsync(
                injury.BodyParts,
                dto => dto.AppointmentInjuryDetailId = createdInjury.Id,
                dto => _bodyParts.CreateAsync(dto));
        }
    }

    /// <summary>
    /// One optional child. Routed through a helper rather than repeated per group so the three
    /// single-row groups do not become three near-identical blocks.
    /// </summary>
    private static async Task<int> WriteSingleAsync<TDto>(
        TDto? dto,
        Action<TDto> assignParent,
        Func<TDto, Task> create)
        where TDto : class
    {
        if (dto is null)
        {
            return 0;
        }

        assignParent(dto);
        await create(dto);
        return 1;
    }

    private static async Task<int> WriteManyAsync<TDto>(
        IReadOnlyCollection<TDto>? items,
        Action<TDto> assignParent,
        Func<TDto, Task> create)
        where TDto : class
    {
        if (items is null || items.Count == 0)
        {
            return 0;
        }

        var written = 0;
        foreach (var item in items)
        {
            assignParent(item);
            await create(item);
            written++;
        }

        return written;
    }
}
