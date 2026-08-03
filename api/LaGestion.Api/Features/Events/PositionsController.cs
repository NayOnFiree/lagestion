using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Events;

public sealed record SavePositionRequest(
    string Label,
    int Headcount,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal HourlyRate,
    string? DressCode,
    string? Brief);

/// <summary>
/// Poste enregistré, accompagné de ce qu'il faut savoir des prestataires déjà
/// engagés dessus.
/// </summary>
/// <param name="ImpactedContractors">
/// Prestataires ayant accepté ou confirmé ce poste et dont le tarif ou les
/// horaires viennent de changer. À prévenir : ils ont dit oui à autre chose.
/// </param>
public sealed record SavedPosition(
    PositionDetail Position,
    IReadOnlyList<string> ImpactedContractors);

[ApiController]
[Authorize(Policy = "admin")]
public sealed class PositionsController(LaGestionDbContext db) : ControllerBase
{
    /// <summary>Ajoute un poste à un événement.</summary>
    [HttpPost("events/{eventId:guid}/positions")]
    [ProducesResponseType<SavedPosition>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedPosition>> Create(
        Guid eventId,
        SavePositionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidate(request))
        {
            return ValidationProblem(ModelState);
        }

        var parent = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (parent is null)
        {
            return NotFound();
        }

        var position = new Position
        {
            EventId = parent.Id,
            Label = request.Label.Trim(),
            Headcount = request.Headcount,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            HourlyRate = request.HourlyRate,
            DressCode = Normalise(request.DressCode),
            Brief = Normalise(request.Brief),
        };

        db.Positions.Add(position);
        await db.SaveChangesAsync(cancellationToken);

        return Created($"/events/{parent.Id}", new SavedPosition(ToDetail(position, 0), []));
    }

    /// <summary>
    /// Met à jour un poste.
    ///
    /// Le tarif et les horaires restent modifiables même après acceptation :
    /// c'est un choix assumé. La réponse signale alors qui a dit oui aux
    /// anciennes conditions, pour que l'agence puisse les prévenir.
    /// </summary>
    [HttpPut("positions/{id:guid}")]
    [ProducesResponseType<SavedPosition>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedPosition>> Update(
        Guid id,
        SavePositionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidate(request))
        {
            return ValidationProblem(ModelState);
        }

        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        var conditionsChanged =
            position.HourlyRate != request.HourlyRate
            || position.StartsAt != request.StartsAt
            || position.EndsAt != request.EndsAt;

        position.Label = request.Label.Trim();
        position.Headcount = request.Headcount;
        position.StartsAt = request.StartsAt;
        position.EndsAt = request.EndsAt;
        position.HourlyRate = request.HourlyRate;
        position.DressCode = Normalise(request.DressCode);
        position.Brief = Normalise(request.Brief);

        await db.SaveChangesAsync(cancellationToken);

        var engaged = await EngagedAsync(position.Id, cancellationToken);

        return Ok(new SavedPosition(
            ToDetail(position, engaged.Count),
            conditionsChanged ? engaged : []));
    }

    /// <summary>Retire un poste, tant que personne n'y a été sollicité.</summary>
    [HttpDelete("positions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        if (await db.Assignments.AnyAsync(a => a.PositionId == id, cancellationToken))
        {
            return Problem(
                title: "Poste déjà proposé",
                detail: "Des prestataires ont été sollicités sur ce poste. Traitez leurs propositions avant de le retirer, ou annulez l'événement.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.Positions.Remove(position);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<List<string>> EngagedAsync(Guid positionId, CancellationToken cancellationToken) =>
        await db.Assignments
            .Where(a => a.PositionId == positionId)
            .Where(a => a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Confirmed)
            .Select(a => a.Contractor!.User!.FirstName + " " + a.Contractor.User.LastName)
            .ToListAsync(cancellationToken);

    private bool TryValidate(SavePositionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            ModelState.AddModelError(nameof(request.Label), "L'intitulé du poste est obligatoire.");
        }

        if (request.Headcount < 1)
        {
            ModelState.AddModelError(nameof(request.Headcount), "Il faut au moins une personne sur un poste.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            ModelState.AddModelError(nameof(request.EndsAt), "La fin doit suivre le début.");
        }

        if (request.HourlyRate < 0)
        {
            ModelState.AddModelError(nameof(request.HourlyRate), "Le tarif horaire ne peut pas être négatif.");
        }

        return ModelState.IsValid;
    }

    private static PositionDetail ToDetail(Position position, int filled) => new(
        position.Id,
        position.Label,
        position.Headcount,
        filled,
        position.StartsAt,
        position.EndsAt,
        position.HourlyRate,
        position.DressCode,
        position.Brief);

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
