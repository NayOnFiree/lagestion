using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Availabilities;

/// <summary>Mission confirmée, telle qu'elle apparaît dans le calendrier.</summary>
public sealed record CalendarMission(
    Guid AssignmentId,
    string EventTitle,
    string PositionLabel,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal HourlyRate);

/// <summary>
/// Un jour du calendrier.
/// </summary>
/// <param name="State">
/// État dominant : <c>confirmed</c>, <c>available</c>, <c>unavailable</c> ou
/// <c>none</c>. Une mission confirmée l'emporte sur tout le reste, une
/// disponibilité l'emporte sur une indisponibilité — le calendrier répond
/// d'abord à « qu'est-ce que je peux encore accepter ce jour-là ».
/// </param>
public sealed record CalendarDay(
    DateOnly Date,
    string State,
    IReadOnlyList<AvailabilitySlot> Slots,
    IReadOnlyList<CalendarMission> Missions);

/// <summary>Compteurs du mois, estimés sur les missions confirmées.</summary>
/// <param name="PlannedHours">Heures prévues, déduites des créneaux des postes.</param>
/// <param name="EstimatedAmount">
/// Rémunération estimée, hors taxes. Estimée et non facturée : les heures
/// réelles ne sont connues qu'après pointage.
/// </param>
public sealed record MonthTotals(int ConfirmedMissions, decimal PlannedHours, decimal EstimatedAmount);

/// <summary>Calendrier d'un mois.</summary>
public sealed record MonthCalendar(int Year, int Month, MonthTotals Totals, IReadOnlyList<CalendarDay> Days);

[ApiController]
[Route("me/calendar")]
[Authorize(Policy = "contractor")]
public sealed class CalendarController(LaGestionDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Calendrier du mois : état de chaque jour et compteurs.
    /// </summary>
    /// <param name="month">Mois au format <c>AAAA-MM</c>. Mois courant par défaut.</param>
    [HttpGet]
    [ProducesResponseType<MonthCalendar>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonthCalendar>> Get(
        [FromQuery] string? month,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var (year, monthNumber) = (today.Year, today.Month);

        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var parsed))
            {
                ModelState.AddModelError(nameof(month), "Mois attendu au format AAAA-MM.");
                return ValidationProblem(ModelState);
            }

            (year, monthNumber) = (parsed.Year, parsed.Month);
        }

        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var contractorId = await db.Contractors
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (contractorId is null)
        {
            return Problem(
                title: "Fiche prestataire introuvable",
                detail: "Ce compte n'est rattaché à aucune fiche prestataire.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var first = new DateOnly(year, monthNumber, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var windowStart = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(last.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var slots = await db.Availabilities
            .Where(a => a.ContractorId == contractorId && a.Date >= first && a.Date <= last)
            .OrderBy(a => a.StartsAt)
            .ToListAsync(cancellationToken);

        var assignments = await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Where(a => a.ContractorId == contractorId && a.Status == AssignmentStatus.Confirmed)
            .Where(a => a.Position!.StartsAt <= windowEnd && a.Position.EndsAt >= windowStart)
            .ToListAsync(cancellationToken);

        var slotsByDay = slots.ToLookup(s => s.Date);
        var days = new List<CalendarDay>(last.Day);

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var daySlots = slotsByDay[date].ToList();

            var dayMissions = assignments
                .Where(a => Covers(a, date))
                .OrderBy(a => a.Position!.StartsAt)
                .Select(a => new CalendarMission(
                    a.Id,
                    a.Position!.Event!.Title,
                    a.Position.Label,
                    a.Position.StartsAt,
                    a.Position.EndsAt,
                    a.Position.HourlyRate))
                .ToList();

            days.Add(new CalendarDay(
                date,
                ResolveState(daySlots, dayMissions),
                daySlots.Select(s => new AvailabilitySlot(s.Id, s.Date, s.StartsAt, s.EndsAt, s.Status.ToString())).ToList(),
                dayMissions));
        }

        // Les compteurs portent sur la mission entière, comptée une seule fois,
        // pas sur chaque jour qu'elle recouvre.
        var plannedHours = assignments.Sum(a => Hours(a.Position!));
        var amount = assignments.Sum(a => Hours(a.Position!) * a.Position!.HourlyRate);

        return Ok(new MonthCalendar(
            year,
            monthNumber,
            new MonthTotals(assignments.Count, decimal.Round(plannedHours, 2), decimal.Round(amount, 2)),
            days));
    }

    private static bool Covers(Assignment assignment, DateOnly date) =>
        DateOnly.FromDateTime(assignment.Position!.StartsAt.UtcDateTime) <= date
        && DateOnly.FromDateTime(assignment.Position.EndsAt.UtcDateTime) >= date;

    private static decimal Hours(Position position) =>
        (decimal)(position.EndsAt - position.StartsAt).TotalHours;

    private static string ResolveState(
        IReadOnlyCollection<Availability> slots,
        IReadOnlyCollection<CalendarMission> missions)
    {
        if (missions.Count > 0)
        {
            return "confirmed";
        }

        if (slots.Any(s => s.Status == AvailabilityStatus.Available))
        {
            return "available";
        }

        return slots.Count > 0 ? "unavailable" : "none";
    }
}
