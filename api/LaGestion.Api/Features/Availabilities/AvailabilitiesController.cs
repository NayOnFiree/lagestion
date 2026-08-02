using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Availabilities;

/// <summary>Créneau déclaré.</summary>
public sealed record AvailabilitySlot(
    Guid Id,
    DateOnly Date,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    string Status);

/// <summary>
/// Déclaration d'un créneau, ou de la journée entière si les heures sont
/// absentes.
/// </summary>
public sealed record DeclareAvailabilityRequest(
    DateOnly Date,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    string Status);

/// <summary>
/// Déclaration récurrente. La récurrence est un confort de saisie : elle se
/// matérialise immédiatement en autant de lignes que de jours concernés.
/// </summary>
/// <param name="Weekdays">
/// Jours concernés, en anglais : <c>Monday</c> … <c>Sunday</c>. Comme partout
/// ailleurs dans l'API, les énumérations circulent en chaînes.
/// </param>
public sealed record DeclareRecurringRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> Weekdays,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    string Status);

/// <summary>Résultat d'une déclaration récurrente.</summary>
/// <param name="Created">Créneaux effectivement créés.</param>
/// <param name="SkippedForConfirmedMission">
/// Jours laissés de côté parce qu'une mission confirmée les couvre.
/// </param>
public sealed record RecurringResult(
    IReadOnlyList<AvailabilitySlot> Created,
    IReadOnlyList<DateOnly> SkippedForConfirmedMission);

[ApiController]
[Route("me/availabilities")]
[Authorize(Policy = "contractor")]
public sealed class AvailabilitiesController(LaGestionDbContext db) : ControllerBase
{
    /// <summary>Horizon maximal d'une déclaration récurrente.</summary>
    private const int MaxRecurringDays = 400;

    /// <summary>Créneaux déclarés sur une période.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AvailabilitySlot>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AvailabilitySlot>>> List(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var contractorId = await FindContractorIdAsync(cancellationToken);

        if (contractorId is null)
        {
            return NoContractorFile();
        }

        var slots = await db.Availabilities
            .Where(a => a.ContractorId == contractorId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.StartsAt)
            .ToListAsync(cancellationToken);

        return Ok(slots.Select(ToSlot).ToList());
    }

    /// <summary>
    /// Déclare un créneau.
    ///
    /// Une déclaration qui en recouvre d'autres les remplace : redéclarer un
    /// créneau, c'est changer d'avis, pas créer un doublon.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AvailabilitySlot>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvailabilitySlot>> Declare(
        DeclareAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadStatus(request.Status, out var status))
        {
            return ValidationProblem(ModelState);
        }

        if (!TryValidateSlot(request.StartsAt, request.EndsAt))
        {
            return ValidationProblem(ModelState);
        }

        var contractorId = await FindContractorIdAsync(cancellationToken);

        if (contractorId is null)
        {
            return NoContractorFile();
        }

        if (status == AvailabilityStatus.Unavailable
            && await HasConfirmedMissionAsync(contractorId.Value, request.Date, request.StartsAt, request.EndsAt, cancellationToken))
        {
            return ConfirmedMissionConflict();
        }

        await ReplaceOverlappingAsync(
            contractorId.Value,
            request.Date,
            request.StartsAt,
            request.EndsAt,
            cancellationToken);

        var slot = new Availability
        {
            ContractorId = contractorId.Value,
            Date = request.Date,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Status = status,
        };

        db.Availabilities.Add(slot);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), ToSlot(slot));
    }

    /// <summary>Déclare le même créneau sur plusieurs jours de la semaine.</summary>
    [HttpPost("recurring")]
    [ProducesResponseType<RecurringResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecurringResult>> DeclareRecurring(
        DeclareRecurringRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadStatus(request.Status, out var status) || !TryValidateSlot(request.StartsAt, request.EndsAt))
        {
            return ValidationProblem(ModelState);
        }

        if (request.To < request.From)
        {
            ModelState.AddModelError(nameof(request.To), "La date de fin précède la date de début.");
            return ValidationProblem(ModelState);
        }

        if (request.To.DayNumber - request.From.DayNumber > MaxRecurringDays)
        {
            ModelState.AddModelError(
                nameof(request.To),
                $"La période ne peut pas dépasser {MaxRecurringDays} jours.");
            return ValidationProblem(ModelState);
        }

        if (request.Weekdays.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Weekdays), "Choisissez au moins un jour de la semaine.");
            return ValidationProblem(ModelState);
        }

        var wanted = new HashSet<DayOfWeek>();

        foreach (var raw in request.Weekdays)
        {
            if (!Enum.TryParse<DayOfWeek>(raw, ignoreCase: true, out var weekday))
            {
                ModelState.AddModelError(
                    nameof(request.Weekdays),
                    $"Jour de la semaine inconnu : « {raw} ». Attendu : Monday à Sunday.");
                return ValidationProblem(ModelState);
            }

            wanted.Add(weekday);
        }

        var contractorId = await FindContractorIdAsync(cancellationToken);

        if (contractorId is null)
        {
            return NoContractorFile();
        }
        var created = new List<Availability>();
        var skipped = new List<DateOnly>();

        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            if (!wanted.Contains(date.DayOfWeek))
            {
                continue;
            }

            // Une mission confirmée n'est pas effacée par une déclaration de
            // masse : le jour est simplement laissé tel quel, et signalé.
            if (status == AvailabilityStatus.Unavailable
                && await HasConfirmedMissionAsync(contractorId.Value, date, request.StartsAt, request.EndsAt, cancellationToken))
            {
                skipped.Add(date);
                continue;
            }

            await ReplaceOverlappingAsync(
                contractorId.Value,
                date,
                request.StartsAt,
                request.EndsAt,
                cancellationToken);

            var slot = new Availability
            {
                ContractorId = contractorId.Value,
                Date = date,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Status = status,
            };

            db.Availabilities.Add(slot);
            created.Add(slot);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new RecurringResult(created.Select(ToSlot).ToList(), skipped));
    }

    /// <summary>Retire un créneau déclaré.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var contractorId = await FindContractorIdAsync(cancellationToken);

        var slot = await db.Availabilities
            .FirstOrDefaultAsync(a => a.Id == id && a.ContractorId == contractorId, cancellationToken);

        if (slot is null)
        {
            return NotFound();
        }

        db.Availabilities.Remove(slot);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Supprime les déclarations que la nouvelle recouvre.</summary>
    private async Task ReplaceOverlappingAsync(
        Guid contractorId,
        DateOnly date,
        TimeOnly? startsAt,
        TimeOnly? endsAt,
        CancellationToken cancellationToken)
    {
        var sameDay = await db.Availabilities
            .Where(a => a.ContractorId == contractorId && a.Date == date)
            .ToListAsync(cancellationToken);

        var overlapping = sameDay.Where(a => a.Overlaps(date, startsAt, endsAt)).ToList();

        if (overlapping.Count > 0)
        {
            db.Availabilities.RemoveRange(overlapping);
        }
    }

    /// <summary>
    /// Vrai si une mission confirmée couvre ce créneau. Les missions sont
    /// datées en instants ; la comparaison se fait sur la journée, ce qui est
    /// suffisant pour empêcher de se déclarer indisponible un jour travaillé.
    /// </summary>
    private async Task<bool> HasConfirmedMissionAsync(
        Guid contractorId,
        DateOnly date,
        TimeOnly? startsAt,
        TimeOnly? endsAt,
        CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(date.ToDateTime(startsAt ?? TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(date.ToDateTime(endsAt ?? TimeOnly.MaxValue), TimeSpan.Zero);

        return await db.Assignments
            .Where(a => a.ContractorId == contractorId)
            .Where(a => a.Status == AssignmentStatus.Confirmed)
            .AnyAsync(a => a.Position!.StartsAt < dayEnd && a.Position.EndsAt > dayStart, cancellationToken);
    }

    private async Task<Guid?> FindContractorIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var contractor = await db.Contractors
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return contractor;
    }

    private bool TryReadStatus(string raw, out AvailabilityStatus status)
    {
        if (Enum.TryParse(raw, out status))
        {
            return true;
        }

        ModelState.AddModelError(
            "status",
            "Statut inconnu. Attendu : Available ou Unavailable. « Confirmé » découle d'une mission, il ne se déclare pas.");

        return false;
    }

    private bool TryValidateSlot(TimeOnly? startsAt, TimeOnly? endsAt)
    {
        if (startsAt is null && endsAt is null)
        {
            return true;
        }

        if (startsAt is null || endsAt is null)
        {
            ModelState.AddModelError("startsAt", "Indiquez les deux heures, ou aucune pour la journée entière.");
            return false;
        }

        if (endsAt <= startsAt)
        {
            ModelState.AddModelError("endsAt", "L'heure de fin doit suivre l'heure de début.");
            return false;
        }

        return true;
    }

    private ActionResult ConfirmedMissionConflict() => Problem(
        title: "Mission confirmée sur ce créneau",
        detail: "Vous avez une mission confirmée à ce moment-là. Contactez l'agence pour vous désister.",
        statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoContractorFile() => Problem(
        title: "Fiche prestataire introuvable",
        detail: "Ce compte n'est rattaché à aucune fiche prestataire.",
        statusCode: StatusCodes.Status404NotFound);

    private static AvailabilitySlot ToSlot(Availability slot) => new(
        slot.Id,
        slot.Date,
        slot.StartsAt,
        slot.EndsAt,
        slot.Status.ToString());
}
