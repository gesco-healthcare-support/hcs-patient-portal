using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decisions 1 + 2) -- staff stop emails to an external person and turn them back on,
/// for one case (office table) or for all cases at every practice (host table). What a stop silences is
/// decided in ONE place, <see cref="EmailStopPolicy"/>; this service only records the stops and shows staff
/// who a case emails. Staff-only by permission; routes on the hand-written <c>EmailStopsController</c>.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.EmailStops.Default)]
public class EmailStopsAppService : CaseEvaluationAppService, IEmailStopsAppService
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentEmailStop, Guid> _caseStopRepository;
    private readonly IRepository<GlobalEmailStop, Guid> _globalStopRepository;
    private readonly AppointmentEmailRecipientsBuilder _recipientsBuilder;
    private readonly EmailStopConsentImpactCalculator _consentImpact;
    private readonly EmailStopAddressGuard _addressGuard;
    private readonly ITenantStore _tenantStore;

    public EmailStopsAppService(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentEmailStop, Guid> caseStopRepository,
        IRepository<GlobalEmailStop, Guid> globalStopRepository,
        AppointmentEmailRecipientsBuilder recipientsBuilder,
        EmailStopConsentImpactCalculator consentImpact,
        EmailStopAddressGuard addressGuard,
        ITenantStore tenantStore)
    {
        _tenantStore = tenantStore;
        _appointmentRepository = appointmentRepository;
        _caseStopRepository = caseStopRepository;
        _globalStopRepository = globalStopRepository;
        _recipientsBuilder = recipientsBuilder;
        _consentImpact = consentImpact;
        _addressGuard = addressGuard;
    }

    public virtual async Task<AppointmentEmailRecipientsDto> GetAppointmentRecipientsAsync(Guid appointmentId)
    {
        await _appointmentRepository.GetAsync(appointmentId);
        var rows = await _recipientsBuilder.BuildAsync(appointmentId);
        var normalized = EmailStopAddress.NormalizeAll(rows.Select(r => r.Email));

        var caseStops = (await _caseStopRepository.GetListAsync(
                x => x.AppointmentId == appointmentId && normalized.Contains(x.NormalizedEmail)))
            .GroupBy(x => x.NormalizedEmail)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StoppedAt).First());
        Dictionary<string, GlobalEmailStop> globalStops;
        using (CurrentTenant.Change(null))
        {
            globalStops = (await _globalStopRepository.GetListAsync(x => normalized.Contains(x.NormalizedEmail)))
                .GroupBy(x => x.NormalizedEmail)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StoppedAt).First());
        }

        foreach (var row in rows)
        {
            var key = EmailStopAddress.Normalize(row.Email)!;
            row.CaseStop = caseStops.TryGetValue(key, out var caseStop) ? ToStatus(caseStop) : null;
            row.AllCasesStop = globalStops.TryGetValue(key, out var globalStop) ? ToStatus(globalStop) : null;
        }
        return new AppointmentEmailRecipientsDto { Items = rows };
    }

    public virtual async Task<EmailStopStatusDto> StopForAppointmentAsync(Guid appointmentId, StopAppointmentEmailsInput input)
    {
        Check.NotNull(input, nameof(input));
        var email = EmailStopAddressGuard.RequireValidEmail(input.Email);
        await _appointmentRepository.GetAsync(appointmentId);
        await _addressGuard.EnsureExternalAsync(email);

        if (input.Scope == EmailStopScope.AllCases)
        {
            return ToStatus(await StopForAllCasesAsync(email, input.Note));
        }

        var normalized = EmailStopAddress.Normalize(email)!;
        var existing = await _caseStopRepository.FirstOrDefaultAsync(
            x => x.AppointmentId == appointmentId && x.NormalizedEmail == normalized);
        if (existing != null)
        {
            return ToStatus(existing);
        }
        var stop = new AppointmentEmailStop(
            GuidGenerator.Create(), CurrentTenant.Id, appointmentId, email,
            CurrentUser.Id, CallerName(), Clock.Now, input.Note);
        await _caseStopRepository.InsertAsync(stop, autoSave: true);
        Logger.LogInformation("EmailStops: an address was stopped for appointment {AppointmentId}.", appointmentId);
        return ToStatus(stop);
    }

    public virtual async Task ResumeForAppointmentAsync(Guid appointmentId, string email)
    {
        var normalized = EmailStopAddress.Normalize(email);
        if (normalized == null)
        {
            return;
        }
        await _appointmentRepository.GetAsync(appointmentId);
        var stops = await _caseStopRepository.GetListAsync(
            x => x.AppointmentId == appointmentId && x.NormalizedEmail == normalized);
        if (stops.Count == 0)
        {
            return;
        }
        await _caseStopRepository.DeleteManyAsync(stops, autoSave: true);
        Logger.LogInformation("EmailStops: an address was turned back on for appointment {AppointmentId}.", appointmentId);
    }

    public virtual async Task<EmailStopConsentImpactDto> GetConsentImpactAsync(
        Guid appointmentId, string email, EmailStopScope scope)
    {
        var address = EmailStopAddressGuard.RequireValidEmail(email);
        await _appointmentRepository.GetAsync(appointmentId);
        return await _consentImpact.CalculateAsync(appointmentId, address, scope);
    }

    public virtual async Task<PagedResultDto<GlobalEmailStopDto>> GetListAsync(GetGlobalEmailStopsInput input)
    {
        Check.NotNull(input, nameof(input));
        using (CurrentTenant.Change(null))
        {
            var query = await _globalStopRepository.GetQueryableAsync();
            var filter = EmailStopAddress.Normalize(input.Filter);
            if (filter != null)
            {
                query = query.Where(x => x.NormalizedEmail.Contains(filter));
            }
            var total = await AsyncExecuter.CountAsync(query);
            var page = await AsyncExecuter.ToListAsync(
                Sort(query, input.Sorting).PageBy(input.SkipCount, input.MaxResultCount));
            return new PagedResultDto<GlobalEmailStopDto>(total, page.Select(ToDto).ToList());
        }
    }

    public virtual async Task<GlobalEmailStopDto> CreateAsync(CreateGlobalEmailStopInput input)
    {
        Check.NotNull(input, nameof(input));
        var email = EmailStopAddressGuard.RequireValidEmail(input.Email);
        await _addressGuard.EnsureExternalAsync(email);
        return ToDto(await StopForAllCasesAsync(email, input.Note));
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        using (CurrentTenant.Change(null))
        {
            var stop = await _globalStopRepository.GetAsync(id);
            await _globalStopRepository.DeleteAsync(stop, autoSave: true);
            Logger.LogInformation("EmailStops: an all-cases stop {StopId} was turned back on.", stop.Id);
        }
    }

    public virtual Task<EmailStopConsentImpactDto> GetGlobalConsentImpactAsync(string email)
    {
        var address = EmailStopAddressGuard.RequireValidEmail(email);
        if (CurrentTenant.Id == null)
        {
            // The staff site holds no cases, so there are no open requests to check from here.
            return Task.FromResult(new EmailStopConsentImpactDto());
        }
        return _consentImpact.CalculateAsync(null, address, EmailStopScope.AllCases);
    }

    // Records an all-cases stop in the host list (idempotent), stamped with the practice it was made from.
    private async Task<GlobalEmailStop> StopForAllCasesAsync(string email, string? note)
    {
        var officeId = CurrentTenant.Id;
        var officeName = CurrentTenant.Name;
        if (officeId.HasValue && string.IsNullOrWhiteSpace(officeName))
        {
            officeName = (await _tenantStore.FindAsync(officeId.Value))?.Name;
        }
        var normalized = EmailStopAddress.Normalize(email)!;
        using (CurrentTenant.Change(null))
        {
            var existing = await _globalStopRepository.FirstOrDefaultAsync(x => x.NormalizedEmail == normalized);
            if (existing != null)
            {
                return existing;
            }
            var stop = new GlobalEmailStop(
                GuidGenerator.Create(), email, CurrentUser.Id, CallerName(), Clock.Now, officeId, officeName, note);
            await _globalStopRepository.InsertAsync(stop, autoSave: true);
            Logger.LogInformation("EmailStops: an all-cases stop {StopId} was recorded.", stop.Id);
            return stop;
        }
    }

    private string? CallerName()
    {
        var name = $"{CurrentUser.Name} {CurrentUser.SurName}".Trim();
        return name.Length > 0 ? name : CurrentUser.UserName;
    }

    private static IQueryable<GlobalEmailStop> Sort(IQueryable<GlobalEmailStop> query, string? sorting)
    {
        var key = (sorting ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "email" or "email asc" => query.OrderBy(x => x.NormalizedEmail),
            "email desc" => query.OrderByDescending(x => x.NormalizedEmail),
            "stoppedat" or "stoppedat asc" => query.OrderBy(x => x.StoppedAt),
            "stoppedbyname" or "stoppedbyname asc" => query.OrderBy(x => x.StoppedByName),
            "stoppedbyname desc" => query.OrderByDescending(x => x.StoppedByName),
            "stoppedfromofficename" or "stoppedfromofficename asc" => query.OrderBy(x => x.StoppedFromOfficeName),
            "stoppedfromofficename desc" => query.OrderByDescending(x => x.StoppedFromOfficeName),
            _ => query.OrderByDescending(x => x.StoppedAt),
        };
    }

    private static EmailStopStatusDto ToStatus(AppointmentEmailStop stop) => new()
    {
        Id = stop.Id,
        Scope = EmailStopScope.Case,
        StoppedByName = stop.StoppedByName,
        StoppedAt = stop.StoppedAt,
        Note = stop.Note,
    };

    private static EmailStopStatusDto ToStatus(GlobalEmailStop stop) => new()
    {
        Id = stop.Id,
        Scope = EmailStopScope.AllCases,
        StoppedByName = stop.StoppedByName,
        StoppedAt = stop.StoppedAt,
        Note = stop.Note,
        StoppedFromOfficeName = stop.StoppedFromOfficeName,
    };

    private static GlobalEmailStopDto ToDto(GlobalEmailStop stop) => new()
    {
        Id = stop.Id,
        Email = stop.Email,
        StoppedByName = stop.StoppedByName,
        StoppedAt = stop.StoppedAt,
        Note = stop.Note,
        StoppedFromOfficeName = stop.StoppedFromOfficeName,
    };
}
