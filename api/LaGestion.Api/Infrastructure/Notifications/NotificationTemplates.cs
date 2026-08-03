using System.Net;
using System.Text.Json;

namespace LaGestion.Api.Infrastructure.Notifications;

/// <summary>Message rendu, prêt à partir.</summary>
public sealed record RenderedEmail(string Subject, string Html, string Text);

/// <summary>
/// Gabarits des mails.
///
/// Volontairement écrits en C# plutôt que confiés à un moteur de rendu : ils
/// sont peu nombreux, courts, et une dépendance de plus ne se justifierait
/// pas. Chaque gabarit produit une version texte et une version HTML — un
/// message qui n'arrive qu'en HTML finit en indésirables.
/// </summary>
public static class NotificationTemplates
{
    public const string MissionProposed = "mission-proposee";
    public const string MissionConfirmed = "mission-confirmee";
    public const string MissionCancelled = "mission-annulee";
    public const string MissionReminder = "mission-rappel";
    public const string DocumentRejected = "document-refuse";
    public const string DocumentExpiring = "document-expirant";
    public const string HoursDisputed = "heures-contestees";
    public const string InvoiceDue = "facture-a-deposer";
    public const string InvoicePaid = "facture-payee";

    public static RenderedEmail Render(string template, string? payloadJson, EmailOptions options)
    {
        var values = Parse(payloadJson);
        var appUrl = options.ContractorAppUrl.TrimEnd('/');

        return template switch
        {
            MissionProposed => Compose(
                $"Proposition de mission : {values.Get("positionLabel")}",
                $"{values.Get("eventTitle")} — {values.Get("when")}.",
                values.Has("deadline")
                    ? $"Réponse attendue avant le {values.Get("deadline")}."
                    : "Répondez dès que possible.",
                $"{appUrl}/missions",
                "Voir la proposition"),

            MissionConfirmed => Compose(
                $"Mission confirmée : {values.Get("positionLabel")}",
                $"{values.Get("eventTitle")} — {values.Get("when")}.",
                "Les modalités d'accès au site sont désormais visibles sur la fiche de mission.",
                $"{appUrl}/missions",
                "Voir la mission"),

            MissionCancelled => Compose(
                $"Mission annulée : {values.Get("positionLabel")}",
                $"{values.Get("eventTitle")} — {values.Get("when")}.",
                "L'agence a annulé cette mission. Votre créneau est de nouveau libre.",
                $"{appUrl}/missions",
                "Voir mes missions"),

            MissionReminder => Compose(
                $"Demain : {values.Get("positionLabel")}",
                $"{values.Get("eventTitle")} — {values.Get("when")}.",
                values.Has("address") ? $"Lieu : {values.Get("address")}." : string.Empty,
                $"{appUrl}/missions",
                "Voir la fiche"),

            DocumentRejected => Compose(
                "Une pièce de votre dossier a été refusée",
                $"Pièce concernée : {values.Get("documentType")}.",
                $"Motif : {values.Get("reason")}",
                $"{appUrl}/documents",
                "Déposer une nouvelle version"),

            DocumentExpiring => Compose(
                values.Get("state") == "expired"
                    ? "Une pièce de votre dossier a expiré"
                    : "Une pièce de votre dossier expire bientôt",
                $"Pièce concernée : {values.Get("documentType")}.",
                values.Get("state") == "expired"
                    ? $"Elle a expiré le {values.Get("expiresAt")}. Sans elle, l'agence ne peut plus vous proposer de mission."
                    : $"Elle expire le {values.Get("expiresAt")}.",
                $"{appUrl}/documents",
                "Mettre à jour mon dossier"),

            HoursDisputed => Compose(
                "Vos heures déclarées sont contestées",
                $"{values.Get("positionLabel")} — {values.Get("when")}.",
                $"Motif : {values.Get("reason")}",
                $"{appUrl}/heures",
                "Revoir ma déclaration"),

            InvoiceDue => Compose(
                "Vous avez des prestations à facturer",
                $"{values.Get("count")} prestation(s) validée(s) sur la période, pour {values.Get("total")}.",
                "L'application pré-remplit votre facture, vous n'avez qu'à vérifier et déposer.",
                $"{appUrl}/factures",
                "Établir ma facture"),

            InvoicePaid => Compose(
                $"Facture {values.Get("number")} payée",
                $"Montant : {values.Get("total")}.",
                "Le règlement a été enregistré par l'agence.",
                $"{appUrl}/factures",
                "Voir mes factures"),

            _ => Compose(
                "Notification",
                "Vous avez une nouvelle notification.",
                string.Empty,
                appUrl,
                "Ouvrir l'application"),
        };
    }

    private static RenderedEmail Compose(
        string subject,
        string lead,
        string detail,
        string url,
        string action)
    {
        var lines = new[] { lead, detail }.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        var text = string.Join("\n\n", [subject, .. lines, $"{action} : {url}"]);

        var paragraphs = string.Join(
            "\n",
            lines.Select(line => $"      <p style=\"margin:0 0 12px\">{WebUtility.HtmlEncode(line)}</p>"));

        var html = $"""
            <!doctype html>
            <html lang="fr">
              <body style="margin:0;background:#f7f8f9;font-family:-apple-system,Segoe UI,Roboto,sans-serif;color:#0f1115">
                <div style="max-width:520px;margin:0 auto;padding:24px">
                  <div style="background:#ffffff;border:1px solid #e6e8eb;border-radius:8px;padding:24px">
                    <h1 style="margin:0 0 16px;font-size:18px">{WebUtility.HtmlEncode(subject)}</h1>
            {paragraphs}
                    <p style="margin:24px 0 0">
                      <a href="{WebUtility.HtmlEncode(url)}"
                         style="display:inline-block;background:#1f6f5c;color:#ffffff;text-decoration:none;padding:10px 16px;border-radius:6px">
                        {WebUtility.HtmlEncode(action)}
                      </a>
                    </p>
                  </div>
                  <p style="margin:16px 0 0;font-size:12px;color:#5b6472">
                    Message automatique, merci de ne pas y répondre.
                  </p>
                </div>
              </body>
            </html>
            """;

        return new RenderedEmail(subject, html, text);
    }

    private static Values Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Values([]);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return new Values(parsed ?? []);
        }
        catch (JsonException)
        {
            return new Values([]);
        }
    }

    private sealed record Values(Dictionary<string, string> Items)
    {
        public string Get(string key) => Items.GetValueOrDefault(key, string.Empty);

        public bool Has(string key) => !string.IsNullOrWhiteSpace(Get(key));
    }
}
