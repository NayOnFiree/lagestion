using LaGestion.Api.Domain;
using LaGestion.Api.Features.Documents;
using LaGestion.Api.Infrastructure;
using LaGestion.Api.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Staffing;

/// <summary>Prestataire proposable sur un poste.</summary>
/// <param name="DossierComplete">
/// Faux si des pièces manquent. Non bloquant : l'agence arbitre, mais elle
/// doit le savoir avant de proposer.
/// </param>
public sealed record Candidate(
    Guid ContractorId,
    string Name,
    string Email,
    string? BaseCity,
    int? TravelRadiusKm,
    decimal? DefaultHourlyRate,
    IReadOnlyList<string> Skills,
    bool DossierComplete,
    IReadOnlyList<string> MissingDocumentTypes);

/// <summary>Proposition envoyée, vue de l'agence.</summary>
public sealed record AssignmentRow(
    Guid Id,
    Guid ContractorId,
    string ContractorName,
    string Status,
    bool IsExpired,
    DateTimeOffset ProposedAt,
    DateTimeOffset? ResponseDeadline,
    DateTimeOffset? RespondedAt);

/// <summary>État de staffing d'un poste.</summary>
public sealed record PositionStaffing(
    Guid PositionId,
    string Label,
    string EventTitle,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal HourlyRate,
    int Headcount,
    int ConfirmedCount,
    IReadOnlyList<AssignmentRow> Assignments);

/// <param name="ContractorIds">Prestataires à solliciter.</param>
/// <param name="ResponseDeadline">Date limite de réponse laissée à chacun.</param>
public sealed record ProposeRequest(IReadOnlyList<Guid> ContractorIds, DateTimeOffset? ResponseDeadline);

[ApiController]
[Authorize(Policy = "admin")]
public sealed class StaffingController(
    LaGestionDbContext db,
    TimeProvider timeProvider,
    NotificationQueue notifications) : ControllerBase
{
    /// <summary>
    /// Prestataires proposables sur un poste : déclarés disponibles sur tout
    /// le créneau, pas déjà sollicités, sans mission confirmée en conflit.
    /// </summary>
    /// <param name="skill">Filtre facultatif sur une compétence.</param>
    [HttpGet("positions/{id:guid}/candidates")]
    [ProducesResponseType<IReadOnlyList<Candidate>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Candidate>>> Candidates(
        Guid id,
        [FromQuery] string? skill,
        CancellationToken cancellationToken)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        var alreadyAsked = await db.Assignments
            .Where(a => a.PositionId == id && a.Status != AssignmentStatus.Cancelled)
            .Select(a => a.ContractorId)
            .ToListAsync(cancellationToken);

        var contractors = await db.Contractors
            .Include(c => c.User)
            .Include(c => c.Skills)
            .ThenInclude(cs => cs.Skill)
            .Where(c => !alreadyAsked.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(skill))
        {
            contractors = contractors
                .Where(c => c.Skills.Any(s => s.Skill!.Name.Equals(skill, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var contractorIds = contractors.Select(c => c.Id).ToList();
        var firstDay = DateOnly.FromDateTime(position.StartsAt.UtcDateTime);
        var lastDay = DateOnly.FromDateTime(position.EndsAt.UtcDateTime);

        var declarations = await db.Availabilities
            .Where(a => contractorIds.Contains(a.ContractorId))
            .Where(a => a.Date >= firstDay && a.Date <= lastDay)
            .ToListAsync(cancellationToken);

        var documents = await db.Documents
            .Where(d => contractorIds.Contains(d.ContractorId))
            .ToListAsync(cancellationToken);

        var byContractor = declarations.ToLookup(a => a.ContractorId);
        var documentsByContractor = documents.ToLookup(d => d.ContractorId);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var result = new List<Candidate>();

        foreach (var contractor in contractors)
        {
            if (!StaffingRules.CoversSlot(
                    byContractor[contractor.Id].ToList(),
                    position.StartsAt,
                    position.EndsAt))
            {
                continue;
            }

            if (await StaffingRules.HasConfirmedConflictAsync(
                    db, contractor.Id, position.StartsAt, position.EndsAt, null, cancellationToken))
            {
                continue;
            }

            var completeness = DossierRules.Evaluate(
                contractor,
                documentsByContractor[contractor.Id].ToList(),
                today);

            result.Add(new Candidate(
                contractor.Id,
                $"{contractor.User!.FirstName} {contractor.User.LastName}",
                contractor.User.Email,
                contractor.BaseCity,
                contractor.TravelRadiusKm,
                contractor.DefaultHourlyRate,
                contractor.Skills.Select(s => s.Skill!.Name).OrderBy(name => name).ToList(),
                completeness.IsComplete,
                completeness.MissingDocumentTypes));
        }

        // Dossiers complets d'abord : à disponibilité égale, c'est le
        // prestataire qu'on peut envoyer sans rien relancer.
        return Ok(result
            .OrderByDescending(c => c.DossierComplete)
            .ThenBy(c => c.Name)
            .ToList());
    }

    /// <summary>État de staffing d'un poste et propositions déjà envoyées.</summary>
    [HttpGet("positions/{id:guid}/staffing")]
    [ProducesResponseType<PositionStaffing>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionStaffing>> Staffing(Guid id, CancellationToken cancellationToken)
    {
        var position = await db.Positions
            .Include(p => p.Event)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        return Ok(await ToStaffingAsync(position, cancellationToken));
    }

    /// <summary>Envoie une proposition à plusieurs prestataires d'un coup.</summary>
    [HttpPost("positions/{id:guid}/assignments")]
    [ProducesResponseType<PositionStaffing>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionStaffing>> Propose(
        Guid id,
        ProposeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContractorIds.Count == 0)
        {
            ModelState.AddModelError(nameof(request.ContractorIds), "Choisissez au moins un prestataire.");
            return ValidationProblem(ModelState);
        }

        var now = timeProvider.GetUtcNow();

        if (request.ResponseDeadline is { } deadline && deadline <= now)
        {
            ModelState.AddModelError(nameof(request.ResponseDeadline), "La date limite est déjà passée.");
            return ValidationProblem(ModelState);
        }

        var position = await db.Positions
            .Include(p => p.Event)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        if (position.Event!.Status == EventStatus.Cancelled)
        {
            return Problem(
                title: "Événement annulé",
                detail: "On ne sollicite personne sur un événement annulé.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var existing = await db.Assignments
            .Where(a => a.PositionId == id)
            .ToListAsync(cancellationToken);

        var recipients = await db.Contractors
            .Include(c => c.User)
            .Where(c => request.ContractorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.User!, cancellationToken);

        foreach (var contractorId in request.ContractorIds.Distinct())
        {
            // Une proposition encore en jeu ne se double pas ; une refusée ou
            // annulée peut être relancée.
            if (existing.Any(a => a.ContractorId == contractorId && StaffingRules.IsLive(a.Status)))
            {
                continue;
            }

            db.Assignments.Add(new Assignment
            {
                PositionId = id,
                ContractorId = contractorId,
                Status = AssignmentStatus.Proposed,
                ProposedAt = now,
                ResponseDeadline = request.ResponseDeadline,
            });

            if (recipients.TryGetValue(contractorId, out var user))
            {
                notifications.Enqueue(
                    position.AgencyId,
                    user,
                    NotificationTemplates.MissionProposed,
                    new Dictionary<string, string>
                    {
                        ["positionLabel"] = position.Label,
                        ["eventTitle"] = position.Event.Title,
                        ["when"] = NotificationWorker.Describe(position.StartsAt, position.EndsAt),
                        ["deadline"] = request.ResponseDeadline?.ToLocalTime().ToString("dd/MM/yyyy") ?? string.Empty,
                    });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(await ToStaffingAsync(position, cancellationToken));
    }

    /// <summary>Confirme une proposition acceptée.</summary>
    [HttpPost("assignments/{id:guid}/confirm")]
    [ProducesResponseType<PositionStaffing>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionStaffing>> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status != AssignmentStatus.Accepted)
        {
            return Problem(
                title: "Proposition non acceptée",
                detail: "Seule une proposition acceptée par le prestataire peut être confirmée.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var occupied = await StaffingRules.CountOccupiedAsync(db, assignment.PositionId, cancellationToken);

        if (occupied >= assignment.Position!.Headcount)
        {
            var places = assignment.Position.Headcount == 1
                ? "La seule place est pourvue"
                : $"Les {assignment.Position.Headcount} places sont pourvues";

            return Problem(
                title: "Poste complet",
                detail: $"{places}. Annulez une confirmation avant d'en ajouter une.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await StaffingRules.HasConfirmedConflictAsync(
                db,
                assignment.ContractorId,
                assignment.Position.StartsAt,
                assignment.Position.EndsAt,
                assignment.Id,
                cancellationToken))
        {
            return Problem(
                title: "Prestataire déjà engagé",
                detail: "Ce prestataire a déjà une mission confirmée sur ce créneau.",
                statusCode: StatusCodes.Status409Conflict);
        }

        assignment.Status = AssignmentStatus.Confirmed;

        var contractor = await db.Contractors
            .Include(c => c.User)
            .FirstAsync(c => c.Id == assignment.ContractorId, cancellationToken);

        notifications.Enqueue(
            assignment.AgencyId,
            contractor.User!,
            NotificationTemplates.MissionConfirmed,
            new Dictionary<string, string>
            {
                ["positionLabel"] = assignment.Position.Label,
                ["eventTitle"] = assignment.Position.Event!.Title,
                ["when"] = NotificationWorker.Describe(
                    assignment.Position.StartsAt,
                    assignment.Position.EndsAt),
            });

        await db.SaveChangesAsync(cancellationToken);

        return Ok(await ToStaffingAsync(assignment.Position, cancellationToken));
    }

    /// <summary>
    /// Annule une proposition, à n'importe quel stade. Sur une mission
    /// confirmée, c'est le désistement : la place se libère aussitôt et les
    /// candidats restants redeviennent proposables.
    /// </summary>
    [HttpPost("assignments/{id:guid}/cancel")]
    [ProducesResponseType<PositionStaffing>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionStaffing>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        // On ne prévient que ceux qui s'étaient engagés : annuler une simple
        // proposition sans réponse n'appelle pas de message.
        var wasEngaged = assignment.Status
            is AssignmentStatus.Accepted or AssignmentStatus.Confirmed;

        assignment.Status = AssignmentStatus.Cancelled;

        if (wasEngaged)
        {
            var contractor = await db.Contractors
                .Include(c => c.User)
                .FirstAsync(c => c.Id == assignment.ContractorId, cancellationToken);

            notifications.Enqueue(
                assignment.AgencyId,
                contractor.User!,
                NotificationTemplates.MissionCancelled,
                new Dictionary<string, string>
                {
                    ["positionLabel"] = assignment.Position!.Label,
                    ["eventTitle"] = assignment.Position.Event!.Title,
                    ["when"] = NotificationWorker.Describe(
                        assignment.Position.StartsAt,
                        assignment.Position.EndsAt),
                });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(await ToStaffingAsync(assignment.Position!, cancellationToken));
    }

    private async Task<PositionStaffing> ToStaffingAsync(Position position, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var assignments = await db.Assignments
            .Include(a => a.Contractor!)
            .ThenInclude(c => c.User)
            .Where(a => a.PositionId == position.Id)
            .OrderBy(a => a.ProposedAt)
            .ToListAsync(cancellationToken);

        return new PositionStaffing(
            position.Id,
            position.Label,
            position.Event?.Title ?? string.Empty,
            position.StartsAt,
            position.EndsAt,
            position.HourlyRate,
            position.Headcount,
            assignments.Count(a => StaffingRules.OccupiesSlot(a.Status)),
            assignments.Select(a => new AssignmentRow(
                a.Id,
                a.ContractorId,
                $"{a.Contractor!.User!.FirstName} {a.Contractor.User.LastName}",
                a.Status.ToString(),
                StaffingRules.IsExpired(a, now),
                a.ProposedAt,
                a.ResponseDeadline,
                a.RespondedAt)).ToList());
    }
}
