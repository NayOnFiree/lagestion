using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Events;

/// <summary>Poste d'un événement.</summary>
/// <param name="FilledCount">Propositions acceptées ou confirmées sur ce poste.</param>
public sealed record PositionDetail(
    Guid Id,
    string Label,
    int Headcount,
    int FilledCount,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal HourlyRate,
    string? DressCode,
    string? Brief);

/// <summary>Événement en liste.</summary>
public sealed record EventSummary(
    Guid Id,
    string Title,
    string? ClientName,
    bool IsConfidential,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Address,
    int PositionCount,
    int Headcount,
    int FilledCount);

/// <summary>Événement et ses postes.</summary>
public sealed record EventDetail(
    Guid Id,
    string Title,
    string? ClientName,
    bool IsConfidential,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Address,
    string? AccessNotes,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<PositionDetail> Positions);

public sealed record SaveEventRequest(
    string Title,
    string? ClientName,
    bool IsConfidential,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Address,
    string? AccessNotes,
    string Status);

/// <param name="StartsAt">Nouvelle date de début. Tout le reste est décalé d'autant.</param>
public sealed record DuplicateEventRequest(DateTimeOffset StartsAt, string? Title);

[ApiController]
[Route("events")]
[Authorize(Policy = "admin")]
public sealed class EventsController(LaGestionDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Événements de l'agence.</summary>
    /// <param name="from">Début de la fenêtre. Par défaut, aujourd'hui.</param>
    /// <param name="to">Fin de la fenêtre. Sans limite par défaut.</param>
    /// <param name="status">Filtre facultatif : Draft, Published ou Cancelled.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EventSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventSummary>>> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = db.Events.Include(e => e.Positions).AsQueryable();

        // Par défaut on regarde devant : le back-office sert à préparer.
        var start = from ?? timeProvider.GetUtcNow().AddDays(-1);
        query = query.Where(e => e.EndsAt >= start);

        if (to is { } end)
        {
            query = query.Where(e => e.StartsAt <= end);
        }

        if (Enum.TryParse<EventStatus>(status, out var wanted))
        {
            query = query.Where(e => e.Status == wanted);
        }

        var events = await query.OrderBy(e => e.StartsAt).ToListAsync(cancellationToken);
        var filled = await CountFilledAsync(events.SelectMany(e => e.Positions).Select(p => p.Id), cancellationToken);

        return Ok(events.Select(e => new EventSummary(
            e.Id,
            e.Title,
            e.ClientName,
            e.IsConfidential,
            e.Status.ToString(),
            e.StartsAt,
            e.EndsAt,
            e.Address,
            e.Positions.Count,
            e.Positions.Sum(p => p.Headcount),
            e.Positions.Sum(p => filled.GetValueOrDefault(p.Id)))).ToList());
    }

    /// <summary>Événement et ses postes.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        var found = await db.Events
            .Include(e => e.Positions.OrderBy(p => p.StartsAt))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        return Ok(await ToDetailAsync(found, cancellationToken));
    }

    /// <summary>Crée un événement.</summary>
    [HttpPost]
    [ProducesResponseType<EventDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventDetail>> Create(
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var status))
        {
            return ValidationProblem(ModelState);
        }

        var created = new Event
        {
            Title = request.Title.Trim(),
            ClientName = Normalise(request.ClientName),
            IsConfidential = request.IsConfidential,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Address = Normalise(request.Address),
            AccessNotes = Normalise(request.AccessNotes),
            Status = status,
        };

        db.Events.Add(created);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, await ToDetailAsync(created, cancellationToken));
    }

    /// <summary>Met à jour un événement.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<EventDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetail>> Update(
        Guid id,
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidate(request, out var status))
        {
            return ValidationProblem(ModelState);
        }

        var found = await db.Events
            .Include(e => e.Positions.OrderBy(p => p.StartsAt))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        found.Title = request.Title.Trim();
        found.ClientName = Normalise(request.ClientName);
        found.IsConfidential = request.IsConfidential;
        found.StartsAt = request.StartsAt;
        found.EndsAt = request.EndsAt;
        found.Address = Normalise(request.Address);
        found.AccessNotes = Normalise(request.AccessNotes);

        // L'annulation passe par son propre endpoint : elle horodate et se
        // lit comme une décision, pas comme un champ modifié au passage.
        if (found.Status != EventStatus.Cancelled)
        {
            found.Status = status;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(await ToDetailAsync(found, cancellationToken));
    }

    /// <summary>
    /// Annule un événement. Rien n'est supprimé : l'événement a existé et des
    /// prestataires ont pu être sollicités.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<EventDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetail>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var found = await db.Events
            .Include(e => e.Positions.OrderBy(p => p.StartsAt))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        if (found.Status != EventStatus.Cancelled)
        {
            found.Status = EventStatus.Cancelled;
            found.CancelledAt = timeProvider.GetUtcNow();

            // Les propositions en cours n'ont plus d'objet. Elles ne sont pas
            // supprimées : leur historique reste lisible.
            var assignments = await db.Assignments
                .Where(a => found.Positions.Select(p => p.Id).Contains(a.PositionId))
                .Where(a => a.Status != AssignmentStatus.Declined)
                .ToListAsync(cancellationToken);

            foreach (var assignment in assignments)
            {
                assignment.Status = AssignmentStatus.Cancelled;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(await ToDetailAsync(found, cancellationToken));
    }

    /// <summary>
    /// Duplique un événement et ses postes à une nouvelle date.
    ///
    /// Les propositions ne sont jamais recopiées : dupliquer un événement ne
    /// resollicite personne à son insu.
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType<EventDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetail>> Duplicate(
        Guid id,
        DuplicateEventRequest request,
        CancellationToken cancellationToken)
    {
        var source = await db.Events
            .Include(e => e.Positions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (source is null)
        {
            return NotFound();
        }

        // Tout est décalé du même écart : les durées et les enchaînements
        // entre postes sont conservés tels quels.
        var shift = request.StartsAt - source.StartsAt;

        var copy = new Event
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"{source.Title} (copie)" : request.Title.Trim(),
            ClientName = source.ClientName,
            IsConfidential = source.IsConfidential,
            StartsAt = source.StartsAt + shift,
            EndsAt = source.EndsAt + shift,
            Address = source.Address,
            AccessNotes = source.AccessNotes,
            Status = EventStatus.Draft,
        };

        db.Events.Add(copy);

        foreach (var position in source.Positions)
        {
            copy.Positions.Add(new Position
            {
                AgencyId = copy.AgencyId,
                Label = position.Label,
                Headcount = position.Headcount,
                StartsAt = position.StartsAt + shift,
                EndsAt = position.EndsAt + shift,
                HourlyRate = position.HourlyRate,
                DressCode = position.DressCode,
                Brief = position.Brief,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = copy.Id }, await ToDetailAsync(copy, cancellationToken));
    }

    private bool TryValidate(SaveEventRequest request, out EventStatus status)
    {
        status = EventStatus.Draft;

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "L'intitulé est obligatoire.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            ModelState.AddModelError(nameof(request.EndsAt), "La fin doit suivre le début.");
        }

        if (!Enum.TryParse(request.Status, out status))
        {
            ModelState.AddModelError(nameof(request.Status), "Statut inconnu.");
        }

        return ModelState.IsValid;
    }

    /// <summary>Places déjà pourvues, par poste.</summary>
    private async Task<Dictionary<Guid, int>> CountFilledAsync(
        IEnumerable<Guid> positionIds,
        CancellationToken cancellationToken)
    {
        var ids = positionIds.ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Assignments
            .Where(a => ids.Contains(a.PositionId))
            .Where(a => a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Confirmed)
            .GroupBy(a => a.PositionId)
            .Select(group => new { PositionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.PositionId, x => x.Count, cancellationToken);
    }

    private async Task<EventDetail> ToDetailAsync(Event source, CancellationToken cancellationToken)
    {
        var filled = await CountFilledAsync(source.Positions.Select(p => p.Id), cancellationToken);

        return new EventDetail(
            source.Id,
            source.Title,
            source.ClientName,
            source.IsConfidential,
            source.Status.ToString(),
            source.StartsAt,
            source.EndsAt,
            source.Address,
            source.AccessNotes,
            source.CancelledAt,
            source.Positions
                .OrderBy(p => p.StartsAt)
                .Select(p => new PositionDetail(
                    p.Id,
                    p.Label,
                    p.Headcount,
                    filled.GetValueOrDefault(p.Id),
                    p.StartsAt,
                    p.EndsAt,
                    p.HourlyRate,
                    p.DressCode,
                    p.Brief))
                .ToList());
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
