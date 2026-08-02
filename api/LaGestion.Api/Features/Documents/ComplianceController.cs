using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using LaGestion.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Documents;

/// <summary>Pièce à traiter, vue du back-office.</summary>
/// <param name="DaysUntilExpiry">
/// Jours restants avant expiration. Négatif si la pièce est déjà périmée,
/// nul si elle ne porte pas de date de validité.
/// </param>
public sealed record ComplianceDocument(
    Guid Id,
    Guid ContractorId,
    string ContractorName,
    string Type,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateOnly? IssuedAt,
    DateOnly? ExpiresAt,
    bool IsExpired,
    int? DaysUntilExpiry,
    string Status,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

/// <summary>Conformité documentaire d'un prestataire.</summary>
/// <param name="ExpiringSoonCount">
/// Pièces expirant dans les 30 jours, pièces déjà périmées exclues.
/// </param>
public sealed record ContractorCompliance(
    Guid ContractorId,
    string ContractorName,
    string Email,
    DossierCompleteness Completeness,
    int PendingCount,
    int ExpiredCount,
    int ExpiringSoonCount);

/// <summary>Décision de l'agence sur une pièce.</summary>
public sealed record ReviewDocumentRequest(bool Approved, string? Note);

[ApiController]
[Route("compliance")]
[Authorize(Policy = "admin")]
public sealed class ComplianceController(
    LaGestionDbContext db,
    DocumentLinkSigner linkSigner,
    TimeProvider timeProvider,
    LinkGenerator linkGenerator) : ControllerBase
{
    /// <summary>Fenêtre de relance : au-delà, l'expiration n'est pas encore un sujet.</summary>
    private const int ExpiringSoonDays = 30;

    /// <summary>Conformité de tous les prestataires de l'agence.</summary>
    [HttpGet("contractors")]
    [ProducesResponseType<IReadOnlyList<ContractorCompliance>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContractorCompliance>>> Contractors(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var contractors = await db.Contractors
            .Include(c => c.User)
            .ToListAsync(cancellationToken);

        var documents = await db.Documents.ToListAsync(cancellationToken);
        var byContractor = documents.ToLookup(d => d.ContractorId);

        var result = contractors
            .Select(contractor =>
            {
                var own = byContractor[contractor.Id].ToList();

                return new ContractorCompliance(
                    contractor.Id,
                    $"{contractor.User!.FirstName} {contractor.User.LastName}",
                    contractor.User.Email,
                    DossierRules.Evaluate(contractor, own, today),
                    own.Count(d => d.Status == DocumentStatus.Pending),
                    own.Count(d => d.IsExpired(today)),
                    own.Count(d => IsExpiringSoon(d, today)));
            })
            .OrderByDescending(c => c.PendingCount + c.ExpiredCount)
            .ThenBy(c => c.ContractorName)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// File de traitement : pièces en attente de décision, périmées ou sur le
    /// point de l'être. C'est la seule liste qu'un admin a besoin de regarder
    /// tous les jours.
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType<IReadOnlyList<ComplianceDocument>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComplianceDocument>>> Queue(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var query = db.Documents.Include(d => d.Contractor!).ThenInclude(c => c.User).AsQueryable();

        var explicitStatus = Enum.TryParse<DocumentStatus>(status, out var wanted);

        if (explicitStatus)
        {
            query = query.Where(d => d.Status == wanted);
        }

        var documents = await query.ToListAsync(cancellationToken);

        // Sans filtre explicite, on ne renvoie que ce qui demande une action :
        // en attente de décision, périmé, ou sur le point de l'être.
        var result = documents
            .Where(d => explicitStatus
                || d.Status == DocumentStatus.Pending
                || d.IsExpired(today)
                || IsExpiringSoon(d, today))
            .Select(d => ToComplianceDocument(d, today))
            // Le plus urgent d'abord : périmé, puis proche de l'expiration.
            .OrderBy(d => d.DaysUntilExpiry ?? int.MaxValue)
            .ThenBy(d => d.CreatedAt)
            .ToList();

        return Ok(result);
    }

    /// <summary>Valide ou refuse une pièce.</summary>
    [HttpPost("documents/{id:guid}/review")]
    [ProducesResponseType<ComplianceDocument>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceDocument>> Review(
        Guid id,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        // Un refus sans motif oblige le prestataire à deviner, et il redépose
        // la même pièce.
        if (!request.Approved && note is null)
        {
            ModelState.AddModelError(nameof(request.Note), "Indiquez le motif du refus.");
            return ValidationProblem(ModelState);
        }

        var document = await db.Documents
            .Include(d => d.Contractor!)
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        document.Status = request.Approved ? DocumentStatus.Approved : DocumentStatus.Rejected;
        document.ReviewNote = note;
        document.ReviewedByUserId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        document.ReviewedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return Ok(ToComplianceDocument(document, today));
    }

    /// <summary>Émet un lien de consultation valable quelques minutes.</summary>
    [HttpPost("documents/{id:guid}/link")]
    [ProducesResponseType<DocumentLink>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentLink>> CreateLink(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return document is null
            ? NotFound()
            : Ok(DocumentLinks.Create(document, linkSigner, linkGenerator, HttpContext));
    }

    private static bool IsExpiringSoon(Document document, DateOnly today) =>
        document.ExpiresAt is { } expiry
        && expiry >= today
        && expiry <= today.AddDays(ExpiringSoonDays);

    private static ComplianceDocument ToComplianceDocument(Document document, DateOnly today) => new(
        document.Id,
        document.ContractorId,
        $"{document.Contractor!.User!.FirstName} {document.Contractor.User.LastName}",
        document.Type.ToString(),
        document.OriginalFileName,
        document.ContentType,
        document.SizeBytes,
        document.IssuedAt,
        document.ExpiresAt,
        document.IsExpired(today),
        document.ExpiresAt is { } expiry ? expiry.DayNumber - today.DayNumber : null,
        document.Status.ToString(),
        document.ReviewNote,
        document.ReviewedAt,
        document.CreatedAt);
}
