using System.Globalization;
using System.Text;
using LaGestion.Api.Domain;
using LaGestion.Api.Features.Documents;
using LaGestion.Api.Infrastructure;
using LaGestion.Api.Infrastructure.Notifications;
using LaGestion.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Billing;

/// <summary>Motif d'annulation d'une facture.</summary>
public sealed record CancelInvoiceRequest(string Reason);

[ApiController]
[Route("invoices")]
[Authorize(Policy = "admin")]
public sealed class BillingReviewController(
    LaGestionDbContext db,
    DocumentLinkSigner linkSigner,
    TimeProvider timeProvider,
    LinkGenerator linkGenerator,
    NotificationQueue notifications) : ControllerBase
{
    /// <summary>Factures reçues par l'agence.</summary>
    /// <param name="status">Filtre facultatif : Submitted, Validated, Paid, Cancelled.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvoiceView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceView>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = InvoicesController.Query(db);

        if (Enum.TryParse<InvoiceStatus>(status, out var wanted))
        {
            query = query.Where(i => i.Status == wanted);
        }
        else
        {
            // Par défaut, l'agence ne voit pas les factures qu'un prestataire a
            // émises sans les déposer : elles ne lui ont pas été transmises.
            query = query.Where(i => i.Status != InvoiceStatus.Issued);
        }

        var invoices = await query
            .OrderBy(i => i.Status == InvoiceStatus.Submitted ? 0 : 1)
            .ThenByDescending(i => i.IssuedAt)
            .ToListAsync(cancellationToken);

        return Ok(invoices.Select(InvoicesController.ToView).ToList());
    }

    /// <summary>Valide une facture déposée.</summary>
    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType<InvoiceView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<InvoiceView>> Validate(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, InvoiceStatus.Submitted, InvoiceStatus.Validated, cancellationToken);

    /// <summary>Marque une facture comme payée.</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType<InvoiceView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<InvoiceView>> Pay(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, InvoiceStatus.Validated, InvoiceStatus.Paid, cancellationToken);

    /// <summary>
    /// Annule une facture. Elle n'est jamais supprimée et son numéro reste
    /// consommé : la séquence du prestataire doit rester sans trou.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<InvoiceView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceView>> Cancel(
        Guid id,
        CancelInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            ModelState.AddModelError(nameof(request.Reason), "Indiquez le motif de l'annulation.");
            return ValidationProblem(ModelState);
        }

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return Problem(
                title: "Facture déjà payée",
                detail: "Une facture payée ne s'annule pas. Passez par un avoir.",
                statusCode: StatusCodes.Status409Conflict);
        }

        invoice.Status = InvoiceStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(InvoicesController.ToView(await ReloadAsync(id, cancellationToken)));
    }

    /// <summary>Lien de consultation du PDF, valable quelques minutes.</summary>
    [HttpPost("{id:guid}/link")]
    [ProducesResponseType<DocumentLink>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentLink>> Link(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return invoice?.PdfKey is null
            ? NotFound()
            : Ok(InvoiceLinks.Create(invoice.PdfKey, linkSigner, linkGenerator, HttpContext));
    }

    /// <summary>
    /// Export comptable des factures, en CSV séparé par points-virgules et
    /// encodé en UTF-8 avec BOM : c'est ce qu'attendent Excel en français et
    /// la plupart des logiciels de comptabilité.
    /// </summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = InvoicesController.Query(db).Where(i => i.Status != InvoiceStatus.Issued);

        if (Enum.TryParse<InvoiceStatus>(status, out var wanted))
        {
            query = query.Where(i => i.Status == wanted);
        }

        var invoices = await query.OrderBy(i => i.IssuedAt).ToListAsync(cancellationToken);
        var fr = CultureInfo.GetCultureInfo("fr-FR");

        var csv = new StringBuilder();
        csv.AppendLine("Numero;Date;Prestataire;SIRET;Periode debut;Periode fin;Montant;Statut;TVA");

        foreach (var invoice in invoices)
        {
            csv.Append(Escape(invoice.Number)).Append(';')
                .Append(invoice.IssuedAt.ToString("dd/MM/yyyy", fr)).Append(';')
                .Append(Escape(invoice.IssuerName)).Append(';')
                .Append(Escape(invoice.IssuerSiret ?? string.Empty)).Append(';')
                .Append(invoice.PeriodStart.ToString("dd/MM/yyyy", fr)).Append(';')
                .Append(invoice.PeriodEnd.ToString("dd/MM/yyyy", fr)).Append(';')
                .Append(invoice.TotalAmount.ToString("0.00", fr)).Append(';')
                .Append(invoice.Status).Append(';')
                .Append(invoice.VatExempt ? "Franchise en base" : "Assujetti")
                .AppendLine();
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
        var stamp = timeProvider.GetUtcNow().ToString("yyyy-MM-dd");

        return File(bytes, "text/csv", $"factures-{stamp}.csv");
    }

    private async Task<ActionResult<InvoiceView>> TransitionAsync(
        Guid id,
        InvoiceStatus expected,
        InvoiceStatus next,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status != expected)
        {
            return Problem(
                title: "Transition impossible",
                detail: $"Cette facture est à l'état {invoice.Status}, l'opération attend {expected}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        invoice.Status = next;

        if (next == InvoiceStatus.Paid)
        {
            invoice.PaidAt = timeProvider.GetUtcNow();

            var contractor = await db.Contractors
                .Include(c => c.User)
                .FirstAsync(c => c.Id == invoice.ContractorId, cancellationToken);

            notifications.Enqueue(
                invoice.AgencyId,
                contractor.User!,
                NotificationTemplates.InvoicePaid,
                new Dictionary<string, string>
                {
                    ["number"] = invoice.Number,
                    ["total"] = $"{invoice.TotalAmount:0.00} €",
                });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(InvoicesController.ToView(await ReloadAsync(id, cancellationToken)));
    }

    private Task<Invoice> ReloadAsync(Guid id, CancellationToken cancellationToken) =>
        InvoicesController.Query(db).FirstAsync(i => i.Id == id, cancellationToken);

    /// <summary>Un point-virgule ou un guillemet dans un champ casserait la colonne.</summary>
    private static string Escape(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
