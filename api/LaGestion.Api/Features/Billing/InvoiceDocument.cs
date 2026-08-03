using System.Globalization;
using LaGestion.Api.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LaGestion.Api.Features.Billing;

/// <summary>
/// Rendu PDF d'une facture.
///
/// Toutes les valeurs viennent des colonnes figées à l'émission, jamais du
/// profil courant : une facture transmise ne change plus, même si le
/// prestataire déménage ensuite.
///
/// Les mentions obligatoires figurent toutes : identité et adresse de
/// l'émetteur, SIRET, identité du client, date, numéro, désignation des
/// prestations, quantité, prix unitaire, total, franchise de TVA, délai de
/// paiement, pénalités de retard et indemnité forfaitaire de recouvrement.
/// </summary>
public sealed class InvoiceDocument(Invoice invoice) : IDocument
{
    /// <summary>Délai de paiement retenu, en jours à compter de l'émission.</summary>
    public const int PaymentTermsDays = 30;

    /// <summary>Indemnité forfaitaire de recouvrement, article D. 441-5 du code de commerce.</summary>
    public const decimal RecoveryIndemnity = 40m;

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily(Fonts.Calibri));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });

    private void ComposeHeader(IContainer container) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(issuer =>
                {
                    issuer.Item().Text(invoice.IssuerName).FontSize(13).SemiBold();

                    if (invoice.IssuerAddress is { } address)
                    {
                        issuer.Item().Text(address);
                    }

                    if (invoice.IssuerSiret is { } siret)
                    {
                        issuer.Item().Text($"SIRET {siret}");
                    }
                });

                row.ConstantItem(220).Column(client =>
                {
                    client.Item().Text("Facturé à").FontSize(9).FontColor(Colors.Grey.Darken1);
                    client.Item().Text(invoice.ClientName).SemiBold();

                    if (invoice.ClientAddress is { } address)
                    {
                        client.Item().Text(address);
                    }

                    if (invoice.ClientSiret is { } siret)
                    {
                        client.Item().Text($"SIRET {siret}");
                    }
                });
            });

            column.Item().PaddingTop(20).Text($"Facture {invoice.Number}").FontSize(16).SemiBold();

            column.Item().Text(text =>
            {
                text.Span("Émise le ").FontColor(Colors.Grey.Darken1);
                text.Span(invoice.IssuedAt.ToString("d MMMM yyyy", Fr));
                text.Span("  ·  Période du ").FontColor(Colors.Grey.Darken1);
                text.Span(invoice.PeriodStart.ToString("d MMMM yyyy", Fr));
                text.Span(" au ").FontColor(Colors.Grey.Darken1);
                text.Span(invoice.PeriodEnd.ToString("d MMMM yyyy", Fr));
            });
        });

    private void ComposeContent(IContainer container) =>
        container.PaddingVertical(20).Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Désignation");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Quantité");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Prix unitaire");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                });

                foreach (var line in invoice.Lines.OrderBy(l => l.Label))
                {
                    table.Cell().Element(BodyCell).Text(line.Label);
                    table.Cell().Element(BodyCell).AlignRight().Text($"{line.Hours.ToString("N2", Fr)} h");
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(line.UnitRate));
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(line.Amount));
                }
            });

            column.Item().PaddingTop(16).AlignRight().Text(text =>
            {
                text.Span("Total à payer  ").FontColor(Colors.Grey.Darken1);
                text.Span(Money(invoice.TotalAmount)).FontSize(13).SemiBold();
            });

            if (invoice.VatExempt)
            {
                column.Item().PaddingTop(4).AlignRight()
                    .Text("TVA non applicable, art. 293 B du CGI")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            }

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                column.Item().PaddingTop(24).Text("FACTURE ANNULÉE")
                    .FontSize(14).SemiBold().FontColor(Colors.Red.Darken2);
            }
        });

    private static void ComposeFooter(IContainer container) =>
        container.Column(column =>
        {
            column.Item().Text(
                    $"Paiement à {PaymentTermsDays} jours à compter de la date d'émission.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            column.Item().Text(
                    "En cas de retard de paiement, une pénalité égale à trois fois le taux d'intérêt légal "
                    + "est exigible, sans qu'un rappel soit nécessaire.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            column.Item().Text(
                    $"Indemnité forfaitaire pour frais de recouvrement : {Money(RecoveryIndemnity)} "
                    + "(art. D. 441-5 du code de commerce).")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });

    private static IContainer HeaderCell(IContainer container) =>
        container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Medium)
            .PaddingVertical(6)
            .DefaultTextStyle(style => style.SemiBold().FontSize(9));

    private static IContainer BodyCell(IContainer container) =>
        container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6);

    private static string Money(decimal amount) => amount.ToString("C2", Fr);
}
