using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Staffing;

/// <summary>Mission vue du prestataire.</summary>
/// <param name="ClientName">
/// Nul si l'événement est confidentiel : le nom du client n'est pas exposé.
/// </param>
/// <param name="AccessNotes">
/// Modalités d'accès, transmises seulement une fois la mission confirmée.
/// </param>
public sealed record Mission(
    Guid Id,
    string Status,
    bool IsExpired,
    string EventTitle,
    string? ClientName,
    string PositionLabel,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal HourlyRate,
    decimal PlannedHours,
    decimal EstimatedAmount,
    string? Address,
    string? AccessNotes,
    string? DressCode,
    string? Brief,
    DateTimeOffset ProposedAt,
    DateTimeOffset? ResponseDeadline,
    DateTimeOffset? RespondedAt);

[ApiController]
[Route("me/missions")]
[Authorize(Policy = "contractor")]
public sealed class MissionsController(LaGestionDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Missions du prestataire connecté.</summary>
    /// <param name="scope">
    /// <c>proposals</c> : propositions en attente de réponse.
    /// <c>upcoming</c> : missions confirmées à venir.
    /// <c>past</c> : missions passées.
    /// Toutes par défaut.
    /// </param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Mission>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Mission>>> List(
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        var contractorId = await FindContractorIdAsync(cancellationToken);

        if (contractorId is null)
        {
            return NoContractorFile();
        }

        var now = timeProvider.GetUtcNow();

        var assignments = await LoadAsync(a => a.ContractorId == contractorId, cancellationToken);

        var missions = assignments
            .Where(a => scope switch
            {
                "proposals" => a.Status == AssignmentStatus.Proposed && !StaffingRules.IsExpired(a, now),
                "upcoming" => a.Status is AssignmentStatus.Accepted or AssignmentStatus.Confirmed
                    && a.Position!.EndsAt >= now,
                "past" => a.Position!.EndsAt < now,
                _ => true,
            })
            .OrderBy(a => a.Position!.StartsAt)
            .Select(a => ToMission(a, now))
            .ToList();

        return Ok(missions);
    }

    /// <summary>Fiche d'une mission.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<Mission>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Mission>> Get(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await FindOwnAsync(id, cancellationToken);

        return assignment is null
            ? NotFound()
            : Ok(ToMission(assignment, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Accepte une proposition.
    ///
    /// L'acceptation vaut candidature : c'est l'agence qui confirme ensuite.
    /// Le créneau n'est donc pas réservé, mais une mission déjà confirmée sur
    /// le même moment interdit d'accepter — on ne peut pas être à deux
    /// endroits à la fois.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType<Mission>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Mission>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await FindOwnAsync(id, cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        var now = timeProvider.GetUtcNow();

        if (assignment.Status != AssignmentStatus.Proposed)
        {
            return AlreadyAnswered(assignment);
        }

        if (StaffingRules.IsExpired(assignment, now))
        {
            return Problem(
                title: "Délai de réponse dépassé",
                detail: "La date limite de réponse est passée. Contactez l'agence si vous êtes toujours disponible.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (assignment.Position!.Event!.Status == EventStatus.Cancelled)
        {
            return Problem(
                title: "Événement annulé",
                detail: "Cet événement a été annulé par l'agence.",
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
                title: "Vous êtes déjà pris",
                detail: "Une mission confirmée occupe déjà ce créneau.",
                statusCode: StatusCodes.Status409Conflict);
        }

        assignment.Status = AssignmentStatus.Accepted;
        assignment.RespondedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToMission(assignment, now));
    }

    /// <summary>Refuse une proposition.</summary>
    [HttpPost("{id:guid}/decline")]
    [ProducesResponseType<Mission>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Mission>> Decline(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await FindOwnAsync(id, cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status != AssignmentStatus.Proposed)
        {
            return AlreadyAnswered(assignment);
        }

        var now = timeProvider.GetUtcNow();

        assignment.Status = AssignmentStatus.Declined;
        assignment.RespondedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToMission(assignment, now));
    }

    private async Task<List<Assignment>> LoadAsync(
        System.Linq.Expressions.Expression<Func<Assignment, bool>> predicate,
        CancellationToken cancellationToken) =>
        await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Where(predicate)
            .Where(a => a.Status != AssignmentStatus.Cancelled)
            .ToListAsync(cancellationToken);

    private async Task<Assignment?> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var contractorId = await FindContractorIdAsync(cancellationToken);

        return await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .FirstOrDefaultAsync(a => a.Id == id && a.ContractorId == contractorId, cancellationToken);
    }

    private async Task<Guid?> FindContractorIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return await db.Contractors
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private ActionResult AlreadyAnswered(Assignment assignment) => Problem(
        title: "Proposition déjà traitée",
        detail: $"Cette proposition n'est plus en attente de réponse (état : {assignment.Status}).",
        statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoContractorFile() => Problem(
        title: "Fiche prestataire introuvable",
        detail: "Ce compte n'est rattaché à aucune fiche prestataire.",
        statusCode: StatusCodes.Status404NotFound);

    private static Mission ToMission(Assignment assignment, DateTimeOffset now)
    {
        var position = assignment.Position!;
        var occasion = position.Event!;
        var hours = (decimal)(position.EndsAt - position.StartsAt).TotalHours;

        // Les modalités d'accès ne partent qu'une fois la mission confirmée :
        // avant, le prestataire n'a pas à connaître les codes du site.
        var confirmed = assignment.Status == AssignmentStatus.Confirmed;

        return new Mission(
            assignment.Id,
            assignment.Status.ToString(),
            StaffingRules.IsExpired(assignment, now),
            occasion.Title,
            occasion.IsConfidential ? null : occasion.ClientName,
            position.Label,
            position.StartsAt,
            position.EndsAt,
            position.HourlyRate,
            decimal.Round(hours, 2),
            decimal.Round(hours * position.HourlyRate, 2),
            occasion.Address,
            confirmed ? occasion.AccessNotes : null,
            position.DressCode,
            position.Brief,
            assignment.ProposedAt,
            assignment.ResponseDeadline,
            assignment.RespondedAt);
    }
}
