using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Features.Documents;
using LaGestion.Api.Infrastructure;
using LaGestion.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace LaGestion.Api.Features.Billing;

/// <summary>Prestation facturable : heures validées, pas encore facturées.</summary>
public sealed record BillableMission(
    Guid TimesheetId,
    Guid AssignmentId,
    string EventTitle,
    string PositionLabel,
    DateTimeOffset StartsAt,
    decimal Hours,
    decimal UnitRate,
    decimal Amount);

/// <summary>Ce que l'application propose de facturer pour une période.</summary>
/// <param name="NextNumber">Numéro que portera la facture si elle est émise.</param>
public sealed record InvoiceDraft(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string NextNumber,
    decimal Total,
    IReadOnlyList<BillableMission> Missions,
    IReadOnlyList<string> Blockers);

public sealed record InvoiceLineView(string Label, decimal Hours, decimal UnitRate, decimal Amount);

public sealed record InvoiceView(
    Guid Id,
    string Number,
    string Status,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTimeOffset IssuedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? PaidAt,
    decimal TotalAmount,
    bool VatExempt,
    string IssuerName,
    string ClientName,
    string ContractorName,
    IReadOnlyList<InvoiceLineView> Lines);

/// <param name="TimesheetIds">Prestations retenues. Toutes celles du brouillon par défaut.</param>
public sealed record IssueInvoiceRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<Guid> TimesheetIds);

[ApiController]
[Route("me/invoices")]
[Authorize(Policy = "contractor")]
public sealed class InvoicesController(
    LaGestionDbContext db,
    IDocumentStorage storage,
    DocumentLinkSigner linkSigner,
    TimeProvider timeProvider,
    LinkGenerator linkGenerator) : ControllerBase
{
    /// <summary>
    /// Ce qu'il y a à facturer sur une période, et le numéro que portera la
    /// facture. Rien n'est persisté : le numéro n'est attribué qu'à l'émission.
    /// </summary>
    /// <param name="month">Mois au format AAAA-MM. Mois précédent par défaut.</param>
    [HttpGet("draft")]
    [ProducesResponseType<InvoiceDraft>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDraft>> Draft(
        [FromQuery] string? month,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var reference = today.AddMonths(-1);

        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var parsed))
            {
                ModelState.AddModelError(nameof(month), "Mois attendu au format AAAA-MM.");
                return ValidationProblem(ModelState);
            }

            reference = parsed;
        }

        var start = new DateOnly(reference.Year, reference.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var contractor = await LoadContractorAsync(cancellationToken);

        if (contractor is null)
        {
            return NoContractorFile();
        }

        var missions = await BillableAsync(contractor.Id, start, end, cancellationToken);

        return Ok(new InvoiceDraft(
            start,
            end,
            NextNumber(contractor),
            missions.Sum(m => m.Amount),
            missions,
            Blockers(contractor, missions)));
    }

    /// <summary>Factures du prestataire connecté.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvoiceView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceView>>> List(CancellationToken cancellationToken)
    {
        var contractorId = (await LoadContractorAsync(cancellationToken))?.Id;

        var invoices = await Query(db)
            .Where(i => i.ContractorId == contractorId)
            .OrderByDescending(i => i.SequenceIndex)
            .ToListAsync(cancellationToken);

        return Ok(invoices.Select(ToView).ToList());
    }

    /// <summary>
    /// Émet la facture : attribue le numéro, fige les mentions et génère le
    /// PDF, le tout dans une seule transaction.
    ///
    /// Numéro et PDF sont indissociables : un numéro consommé sans document
    /// laisserait un trou dans la séquence, et l'inverse produirait un PDF
    /// sans numéro valide.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<InvoiceView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceView>> Issue(
        IssueInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var contractor = await LoadContractorAsync(cancellationToken);

        if (contractor is null)
        {
            return NoContractorFile();
        }

        var billable = await BillableAsync(contractor.Id, request.PeriodStart, request.PeriodEnd, cancellationToken);

        var wanted = request.TimesheetIds.Count == 0
            ? billable
            : billable.Where(m => request.TimesheetIds.Contains(m.TimesheetId)).ToList();

        if (wanted.Count == 0)
        {
            return Problem(
                title: "Rien à facturer",
                detail: "Aucune prestation validée et non facturée sur cette période.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var blockers = Blockers(contractor, wanted);

        if (blockers.Count > 0)
        {
            return Problem(
                title: "Facture incomplète",
                detail: string.Join(" ", blockers),
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = timeProvider.GetUtcNow();

        // L'agence est le client de la facture du prestataire. Elle n'a pas de
        // navigation depuis Contractor : la relation est volontairement sans
        // navigation, l'agence étant un cadre et non une donnée qu'on remonte.
        var agency = await db.Agencies.FirstAsync(a => a.Id == contractor.AgencyId, cancellationToken);

        var invoice = new Invoice
        {
            ContractorId = contractor.Id,
            Number = NextNumber(contractor),
            SequenceIndex = contractor.NextInvoiceSequence,
            IssuerName = $"{contractor.User!.FirstName} {contractor.User.LastName}",
            IssuerAddress = contractor.Address,
            IssuerSiret = contractor.Siret,
            ClientName = agency.Name,
            ClientAddress = agency.Address,
            ClientSiret = agency.Siret,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            IssuedAt = now,
            TotalAmount = wanted.Sum(m => m.Amount),
            VatExempt = true,
            Status = InvoiceStatus.Issued,
        };

        foreach (var mission in wanted)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                AgencyId = contractor.AgencyId,
                AssignmentId = mission.AssignmentId,
                Label = $"{mission.PositionLabel} — {mission.EventTitle} du {mission.StartsAt:dd/MM/yyyy}",
                Hours = mission.Hours,
                UnitRate = mission.UnitRate,
                Amount = mission.Amount,
            });
        }

        contractor.NextInvoiceSequence++;

        db.Invoices.Add(invoice);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var pdf = new InvoiceDocument(invoice).GeneratePdf();
        using var content = new MemoryStream(pdf);

        invoice.PdfKey = await storage.SaveAsync(
            contractor.AgencyId,
            contractor.Id,
            ".pdf",
            content,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(List), ToView(await ReloadAsync(invoice.Id, cancellationToken)));
    }

    /// <summary>Dépose la facture auprès de l'agence.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<InvoiceView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceView>> Submit(Guid id, CancellationToken cancellationToken)
    {
        var contractorId = (await LoadContractorAsync(cancellationToken))?.Id;

        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.ContractorId == contractorId, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status != InvoiceStatus.Issued)
        {
            return Problem(
                title: "Facture déjà transmise",
                detail: $"Cette facture n'est plus à déposer (état : {invoice.Status}).",
                statusCode: StatusCodes.Status409Conflict);
        }

        invoice.Status = InvoiceStatus.Submitted;
        invoice.SubmittedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToView(await ReloadAsync(id, cancellationToken)));
    }

    /// <summary>Lien de téléchargement du PDF, valable quelques minutes.</summary>
    [HttpPost("{id:guid}/link")]
    [ProducesResponseType<DocumentLink>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentLink>> Link(Guid id, CancellationToken cancellationToken)
    {
        var contractorId = (await LoadContractorAsync(cancellationToken))?.Id;

        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.ContractorId == contractorId, cancellationToken);

        return invoice?.PdfKey is null
            ? NotFound()
            : Ok(InvoiceLinks.Create(invoice.PdfKey, linkSigner, linkGenerator, HttpContext));
    }

    // --- Règles partagées ---------------------------------------------------

    /// <summary>
    /// Ce qui empêche d'émettre. Une facture sans SIRET n'est pas conforme, et
    /// une facture sans adresse d'émetteur non plus.
    /// </summary>
    internal static List<string> Blockers(Contractor contractor, IReadOnlyCollection<BillableMission> missions)
    {
        var blockers = new List<string>();

        if (string.IsNullOrWhiteSpace(contractor.Siret))
        {
            blockers.Add("Renseignez votre SIRET dans votre profil : il est obligatoire sur une facture.");
        }

        if (string.IsNullOrWhiteSpace(contractor.Address))
        {
            blockers.Add("Renseignez votre adresse dans votre profil : elle est obligatoire sur une facture.");
        }

        if (contractor.LegalStatus is not (LegalStatus.AutoEntrepreneur or LegalStatus.EntrepriseIndividuelle))
        {
            blockers.Add(
                "L'application ne gère que la franchise en base de TVA. Établissez votre facture "
                + "hors application si vous êtes assujetti.");
        }

        if (missions.Count == 0)
        {
            blockers.Add("Aucune prestation validée à facturer sur cette période.");
        }

        return blockers;
    }

    private async Task<List<BillableMission>> BillableAsync(
        Guid contractorId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(end.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // Une prestation déjà portée par une facture non annulée n'est plus
        // facturable : c'est ce qui empêche de la facturer deux fois.
        var sheets = await db.Timesheets
            .Include(t => t.Assignment!)
            .ThenInclude(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Where(t => t.Assignment!.ContractorId == contractorId)
            .Where(t => t.Status == TimesheetStatus.Validated)
            .Where(t => t.Assignment!.Position!.StartsAt >= from && t.Assignment.Position.StartsAt <= to)
            .Where(t => !db.InvoiceLines.Any(l =>
                l.AssignmentId == t.AssignmentId && l.Invoice!.Status != InvoiceStatus.Cancelled))
            .OrderBy(t => t.Assignment!.Position!.StartsAt)
            .ToListAsync(cancellationToken);

        return sheets.Select(t =>
        {
            var position = t.Assignment!.Position!;

            return new BillableMission(
                t.Id,
                t.AssignmentId,
                position.Event!.Title,
                position.Label,
                position.StartsAt,
                t.ActualHours,
                position.HourlyRate,
                decimal.Round(t.ActualHours * position.HourlyRate, 2));
        }).ToList();
    }

    private static string NextNumber(Contractor contractor) =>
        $"{contractor.InvoicePrefix}{contractor.NextInvoiceSequence}";

    private async Task<Contractor?> LoadContractorAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return await db.Contractors
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    private Task<Invoice> ReloadAsync(Guid id, CancellationToken cancellationToken) =>
        Query(db).FirstAsync(i => i.Id == id, cancellationToken);

    private ActionResult NoContractorFile() => Problem(
        title: "Fiche prestataire introuvable",
        detail: "Ce compte n'est rattaché à aucune fiche prestataire.",
        statusCode: StatusCodes.Status404NotFound);

    internal static IQueryable<Invoice> Query(LaGestionDbContext db) =>
        db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Contractor!)
            .ThenInclude(c => c.User);

    internal static InvoiceView ToView(Invoice invoice) => new(
        invoice.Id,
        invoice.Number,
        invoice.Status.ToString(),
        invoice.PeriodStart,
        invoice.PeriodEnd,
        invoice.IssuedAt,
        invoice.SubmittedAt,
        invoice.PaidAt,
        invoice.TotalAmount,
        invoice.VatExempt,
        invoice.IssuerName,
        invoice.ClientName,
        $"{invoice.Contractor!.User!.FirstName} {invoice.Contractor.User.LastName}",
        invoice.Lines
            .OrderBy(l => l.Label)
            .Select(l => new InvoiceLineView(l.Label, l.Hours, l.UnitRate, l.Amount))
            .ToList());
}

/// <summary>Liens signés vers les PDF de factures.</summary>
internal static class InvoiceLinks
{
    public static DocumentLink Create(
        string pdfKey,
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
                k = pdfKey,
                e = expiresAt.ToUnixTimeSeconds(),
                s = signer.Sign(pdfKey, expiresAt),
            })!;

        return new DocumentLink(url, expiresAt);
    }
}
