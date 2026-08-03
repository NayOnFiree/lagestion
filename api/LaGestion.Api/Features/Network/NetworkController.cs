using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Features.Documents;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Network;

/// <summary>Prestataire du réseau, avec ses indicateurs.</summary>
public sealed record NetworkContractor(
    Guid ContractorId,
    string Name,
    string Email,
    string? BaseCity,
    IReadOnlyList<string> Skills,
    decimal? DefaultHourlyRate,
    bool DossierComplete,
    ContractorScore Score);

/// <summary>Appréciation portée sur une prestation.</summary>
public sealed record MissionRatingView(
    Guid AssignmentId,
    string EventTitle,
    string PositionLabel,
    DateTimeOffset StartsAt,
    int Rating,
    string? Comment,
    DateTimeOffset RatedAt);

/// <summary>Fiche détaillée d'un prestataire.</summary>
public sealed record ContractorProfileDetail(
    NetworkContractor Contractor,
    IReadOnlyList<MissionRatingView> Ratings,
    IReadOnlyList<MissionRatingView> Unrated);

/// <param name="Rating">Note de 1 à 5.</param>
public sealed record RateMissionRequest(int Rating, string? Comment);

[ApiController]
[Route("network")]
[Authorize(Policy = "admin")]
public sealed class NetworkController(LaGestionDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Prestataires du réseau, triés par score décroissant.
    ///
    /// Les prestataires sans historique arrivent en fin de liste plutôt qu'en
    /// tête ou en queue de classement : leur absence de score n'est pas un
    /// mauvais score.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NetworkContractor>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NetworkContractor>>> List(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var contractors = await db.Contractors
            .Include(c => c.User)
            .Include(c => c.Skills)
            .ThenInclude(cs => cs.Skill)
            .ToListAsync(cancellationToken);

        var assignments = await LoadAssignmentsAsync(cancellationToken);
        var cancelledEvents = await CancelledEventIdsAsync(cancellationToken);
        var ratings = await db.MissionRatings.Include(r => r.Assignment).ToListAsync(cancellationToken);
        var documents = await db.Documents.ToListAsync(cancellationToken);

        var byContractor = assignments.ToLookup(a => a.ContractorId);
        var ratingsByContractor = ratings.ToLookup(r => r.Assignment!.ContractorId);
        var documentsByContractor = documents.ToLookup(d => d.ContractorId);

        var result = contractors
            .Select(contractor => Build(
                contractor,
                byContractor[contractor.Id].ToList(),
                cancelledEvents,
                ratingsByContractor[contractor.Id].ToList(),
                documentsByContractor[contractor.Id].ToList(),
                now,
                today))
            .OrderByDescending(c => c.Score.Score ?? -1)
            .ThenBy(c => c.Name)
            .ToList();

        return Ok(result);
    }

    /// <summary>Fiche d'un prestataire : indicateurs, appréciations reçues, prestations non appréciées.</summary>
    [HttpGet("{contractorId:guid}")]
    [ProducesResponseType<ContractorProfileDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractorProfileDetail>> Get(
        Guid contractorId,
        CancellationToken cancellationToken)
    {
        var contractor = await db.Contractors
            .Include(c => c.User)
            .Include(c => c.Skills)
            .ThenInclude(cs => cs.Skill)
            .FirstOrDefaultAsync(c => c.Id == contractorId, cancellationToken);

        if (contractor is null)
        {
            return NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var assignments = (await LoadAssignmentsAsync(cancellationToken))
            .Where(a => a.ContractorId == contractorId)
            .ToList();

        var cancelledEvents = await CancelledEventIdsAsync(cancellationToken);

        var ratings = await db.MissionRatings
            .Include(r => r.Assignment!)
            .ThenInclude(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Where(r => r.Assignment!.ContractorId == contractorId)
            .ToListAsync(cancellationToken);

        var documents = await db.Documents
            .Where(d => d.ContractorId == contractorId)
            .ToListAsync(cancellationToken);

        var rated = ratings.Select(r => r.AssignmentId).ToHashSet();

        // Prestations terminées et confirmées qui n'ont pas encore reçu
        // d'appréciation. Facultatif : rien n'oblige à les remplir.
        var unrated = assignments
            .Where(a => a.Status == AssignmentStatus.Confirmed)
            .Where(a => a.Position!.EndsAt < now)
            .Where(a => !rated.Contains(a.Id))
            .OrderByDescending(a => a.Position!.StartsAt)
            .Select(a => new MissionRatingView(
                a.Id,
                a.Position!.Event!.Title,
                a.Position.Label,
                a.Position.StartsAt,
                0,
                null,
                default))
            .ToList();

        return Ok(new ContractorProfileDetail(
            Build(contractor, assignments, cancelledEvents, ratings, documents, now, today),
            ratings
                .OrderByDescending(r => r.RatedAt)
                .Select(r => new MissionRatingView(
                    r.AssignmentId,
                    r.Assignment!.Position!.Event!.Title,
                    r.Assignment.Position.Label,
                    r.Assignment.Position.StartsAt,
                    r.Rating,
                    r.Comment,
                    r.RatedAt))
                .ToList(),
            unrated));
    }

    /// <summary>
    /// Note une prestation terminée. Facultatif : une mission peut rester sans
    /// appréciation. Noter à nouveau corrige la note précédente.
    /// </summary>
    [HttpPost("assignments/{assignmentId:guid}/rating")]
    [ProducesResponseType<MissionRatingView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MissionRatingView>> Rate(
        Guid assignmentId,
        RateMissionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rating is < MissionRating.MinRating or > MissionRating.MaxRating)
        {
            ModelState.AddModelError(
                nameof(request.Rating),
                $"La note va de {MissionRating.MinRating} à {MissionRating.MaxRating}.");
            return ValidationProblem(ModelState);
        }

        var assignment = await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        var now = timeProvider.GetUtcNow();

        if (assignment.Status != AssignmentStatus.Confirmed || assignment.Position!.EndsAt >= now)
        {
            return Problem(
                title: "Prestation non effectuée",
                detail: "Seule une mission confirmée et terminée peut être appréciée.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var rating = await db.MissionRatings
            .FirstOrDefaultAsync(r => r.AssignmentId == assignmentId, cancellationToken);

        if (rating is null)
        {
            rating = new MissionRating { AssignmentId = assignmentId };
            db.MissionRatings.Add(rating);
        }

        rating.Rating = request.Rating;
        rating.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        rating.RatedByUserId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        rating.RatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new MissionRatingView(
            assignmentId,
            assignment.Position!.Event!.Title,
            assignment.Position.Label,
            assignment.Position.StartsAt,
            rating.Rating,
            rating.Comment,
            rating.RatedAt));
    }

    private Task<List<Assignment>> LoadAssignmentsAsync(CancellationToken cancellationToken) =>
        db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Événements annulés par l'agence. Les missions qu'ils portaient ne
    /// comptent pas comme des désistements.
    /// </summary>
    private async Task<HashSet<Guid>> CancelledEventIdsAsync(CancellationToken cancellationToken) =>
        (await db.Events
            .Where(e => e.Status == EventStatus.Cancelled)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private static NetworkContractor Build(
        Contractor contractor,
        IReadOnlyCollection<Assignment> assignments,
        IReadOnlySet<Guid> cancelledEvents,
        IReadOnlyCollection<MissionRating> ratings,
        IReadOnlyCollection<Document> documents,
        DateTimeOffset now,
        DateOnly today)
    {
        var completeness = DossierRules.Evaluate(contractor, documents, today);

        return new NetworkContractor(
            contractor.Id,
            $"{contractor.User!.FirstName} {contractor.User.LastName}",
            contractor.User.Email,
            contractor.BaseCity,
            contractor.Skills.Select(s => s.Skill!.Name).OrderBy(name => name).ToList(),
            contractor.DefaultHourlyRate,
            completeness.IsComplete,
            ScoreRules.Evaluate(assignments, cancelledEvents, ratings, now));
    }
}
