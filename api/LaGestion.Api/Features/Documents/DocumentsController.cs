using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using LaGestion.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LaGestion.Api.Features.Documents;

/// <summary>Pièce du coffre, telle qu'affichée au prestataire.</summary>
public sealed record DocumentSummary(
    Guid Id,
    string Type,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateOnly? IssuedAt,
    DateOnly? ExpiresAt,
    bool IsExpired,
    string Status,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

/// <summary>Coffre complet : les pièces et l'état du dossier.</summary>
public sealed record DocumentVault(
    IReadOnlyList<DocumentSummary> Documents,
    DossierCompleteness Completeness);

/// <summary>Lien de téléchargement à durée courte.</summary>
public sealed record DocumentLink(string Url, DateTimeOffset ExpiresAt);

/// <summary>Dépôt d'une pièce. Envoyé en multipart.</summary>
public sealed class UploadDocumentRequest
{
    public required string Type { get; init; }

    public DateOnly? IssuedAt { get; init; }

    public DateOnly? ExpiresAt { get; init; }

    public required IFormFile File { get; init; }
}

[ApiController]
[Route("me/documents")]
[Authorize(Policy = "contractor")]
public sealed class DocumentsController(
    LaGestionDbContext db,
    IDocumentStorage storage,
    DocumentLinkSigner linkSigner,
    IOptions<StorageOptions> storageOptions,
    TimeProvider timeProvider,
    LinkGenerator linkGenerator) : ControllerBase
{
    private readonly StorageOptions _storage = storageOptions.Value;

    /// <summary>Coffre à documents et état de complétude du dossier.</summary>
    [HttpGet]
    [ProducesResponseType<DocumentVault>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentVault>> List(CancellationToken cancellationToken)
    {
        var contractor = await LoadContractorAsync(cancellationToken);

        if (contractor is null)
        {
            return NoContractorFile();
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var documents = await db.Documents
            .Where(d => d.ContractorId == contractor.Id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(new DocumentVault(
            documents.Select(d => ToSummary(d, today)).ToList(),
            DossierRules.Evaluate(contractor, documents, today)));
    }

    /// <summary>Dépose une pièce dans le coffre.</summary>
    [HttpPost]
    [ProducesResponseType<DocumentSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentSummary>> Upload(
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DocumentType>(request.Type, out var type))
        {
            ModelState.AddModelError(nameof(request.Type), "Type de document inconnu.");
            return ValidationProblem(ModelState);
        }

        if (request.File.Length == 0)
        {
            ModelState.AddModelError(nameof(request.File), "Le fichier est vide.");
            return ValidationProblem(ModelState);
        }

        if (request.File.Length > _storage.MaxFileSizeBytes)
        {
            ModelState.AddModelError(
                nameof(request.File),
                $"Fichier trop volumineux : {_storage.MaxFileSizeBytes / (1024 * 1024)} Mo au maximum.");
            return ValidationProblem(ModelState);
        }

        if (request.ExpiresAt is { } expiry && request.IssuedAt is { } issued && expiry < issued)
        {
            ModelState.AddModelError(
                nameof(request.ExpiresAt),
                "La date de fin de validité précède la date de délivrance.");
            return ValidationProblem(ModelState);
        }

        var contractor = await LoadContractorAsync(cancellationToken);

        if (contractor is null)
        {
            return NoContractorFile();
        }

        // Le type réel se lit dans les octets. Se fier au Content-Type annoncé
        // ou à l'extension reviendrait à laisser le client décider.
        await using var upload = request.File.OpenReadStream();

        var header = new byte[8];
        var read = await upload.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (AcceptedFileTypes.Detect(header.AsSpan(0, read)) is not { } fileType)
        {
            ModelState.AddModelError(
                nameof(request.File),
                $"Format non accepté. Formats acceptés : {AcceptedFileTypes.HumanReadableList}.");
            return ValidationProblem(ModelState);
        }

        var (contentType, extension) = fileType;
        upload.Position = 0;

        var key = await storage.SaveAsync(
            contractor.AgencyId,
            contractor.Id,
            extension,
            upload,
            cancellationToken);

        var document = new Document
        {
            AgencyId = contractor.AgencyId,
            ContractorId = contractor.Id,
            Type = type,
            FileKey = key,
            OriginalFileName = Path.GetFileName(request.File.FileName),
            ContentType = contentType,
            SizeBytes = request.File.Length,
            IssuedAt = request.IssuedAt,
            ExpiresAt = request.ExpiresAt,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return CreatedAtAction(nameof(List), ToSummary(document, today));
    }

    /// <summary>Retire une pièce du coffre.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var document = await FindOwnDocumentAsync(id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        if (document.Status == DocumentStatus.Approved)
        {
            return Problem(
                title: "Pièce validée",
                detail: "Une pièce validée par l'agence ne se supprime pas : déposez la nouvelle version, elle remplacera l'ancienne.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);

        await storage.DeleteAsync(document.FileKey, cancellationToken);

        return NoContent();
    }

    /// <summary>Émet un lien de téléchargement valable quelques minutes.</summary>
    [HttpPost("{id:guid}/link")]
    [ProducesResponseType<DocumentLink>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentLink>> CreateLink(Guid id, CancellationToken cancellationToken)
    {
        var document = await FindOwnDocumentAsync(id, cancellationToken);

        return document is null
            ? NotFound()
            : Ok(DocumentLinks.Create(document, linkSigner, linkGenerator, HttpContext));
    }

    private async Task<Contractor?> LoadContractorAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return await db.Contractors.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Ne trouve la pièce que si elle appartient au prestataire connecté. Le
    /// filtre d'agence écarte déjà les autres agences ; cette jointure écarte
    /// les autres prestataires de la même agence.
    /// </summary>
    private async Task<Document?> FindOwnDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return await db.Documents
            .Where(d => d.Id == id)
            .Where(d => db.Contractors.Any(c => c.Id == d.ContractorId && c.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private ActionResult NoContractorFile() => Problem(
        title: "Fiche prestataire introuvable",
        detail: "Ce compte n'est rattaché à aucune fiche prestataire.",
        statusCode: StatusCodes.Status404NotFound);

    internal static DocumentSummary ToSummary(Document document, DateOnly today) => new(
        document.Id,
        document.Type.ToString(),
        document.OriginalFileName,
        document.ContentType,
        document.SizeBytes,
        document.IssuedAt,
        document.ExpiresAt,
        document.IsExpired(today),
        document.Status.ToString(),
        document.ReviewNote,
        document.ReviewedAt,
        document.CreatedAt);
}

/// <summary>Construction des liens signés, partagée par les deux côtés.</summary>
internal static class DocumentLinks
{
    public static DocumentLink Create(
        Document document,
        DocumentLinkSigner signer,
        LinkGenerator linkGenerator,
        HttpContext context)
    {
        var expiresAt = signer.NextExpiry;

        var url = linkGenerator.GetUriByAction(
            context,
            action: nameof(DocumentContentController.Download),
            controller: "DocumentContent",
            values: new
            {
                k = document.FileKey,
                e = expiresAt.ToUnixTimeSeconds(),
                s = signer.Sign(document.FileKey, expiresAt),
            })!;

        return new DocumentLink(url, expiresAt);
    }
}

/// <summary>
/// Service du contenu des pièces.
///
/// Volontairement anonyme : un navigateur qui ouvre un lien ou charge une
/// image ne pose pas d'en-tête <c>Authorization</c>. L'autorisation est
/// portée par la signature du lien, valable quelques minutes.
/// </summary>
[ApiController]
[Route("documents/content")]
[AllowAnonymous]
public sealed class DocumentContentController(
    IDocumentStorage storage,
    DocumentLinkSigner linkSigner) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        [FromQuery] string k,
        [FromQuery] long e,
        [FromQuery] string s,
        CancellationToken cancellationToken)
    {
        if (!linkSigner.IsValid(k, e, s))
        {
            // Lien invalide ou périmé : même réponse dans les deux cas, rien
            // ne doit permettre de distinguer une clé existante d'une autre.
            return NotFound();
        }

        var content = await storage.OpenAsync(k, cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(k) switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };

        return File(content, contentType);
    }
}
