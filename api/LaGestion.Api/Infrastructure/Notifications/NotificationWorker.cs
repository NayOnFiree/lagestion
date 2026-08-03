using LaGestion.Api.Domain;
using LaGestion.Api.Features.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LaGestion.Api.Infrastructure.Notifications;

/// <summary>
/// Dépile la file d'envoi et déclenche les rappels périodiques.
///
/// Hébergé dans l'API : rien à installer sur le serveur. Limite assumée — si
/// l'API tourne un jour en plusieurs instances, il faudra un verrou partagé,
/// sans quoi chaque instance enverrait les mêmes rappels. La clé d'unicité
/// des notifications limite déjà la casse, mais ne remplace pas un verrou.
/// </summary>
public sealed class NotificationWorker(
    IServiceScopeFactory scopeFactory,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>Fenêtre au-delà de laquelle une pièce qui expire est signalée.</summary>
    private const int ExpiringSoonDays = 30;

    private DateOnly _lastDailyScan;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisse l'application finir de démarrer avant le premier passage.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

                if (today != _lastDailyScan)
                {
                    await RunDailyScansAsync(stoppingToken);
                    _lastDailyScan = today;
                }

                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                // Un passage raté ne doit pas tuer le service : le suivant
                // reprendra la file là où elle en est.
                logger.LogError(exception, "Passage du service de notifications en échec.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // --- File d'envoi -------------------------------------------------------

    /// <summary>
    /// La fabrique de contexte est enregistrée par portée : un service de
    /// fond, lui, est unique. On ouvre donc une portée à chaque passage.
    /// </summary>
    private AgencyDbContextFactory Factory(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<AgencyDbContextFactory>();

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await using var db = Factory(scope).CreateFor(Guid.Empty);

        // Le filtre d'agence ne laisserait rien passer sur une agence vide :
        // la file est balayée toutes agences confondues, volontairement.
        var pending = await db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.Status == NotificationStatus.Pending)
            .Where(n => n.Attempts < Notification.MaxAttempts)
            .OrderBy(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var notification in pending)
        {
            var message = NotificationTemplates.Render(
                notification.Template,
                notification.Payload,
                emailOptions.Value);

            try
            {
                await emailSender.SendAsync(
                    notification.Recipient,
                    message.Subject,
                    message.Html,
                    message.Text,
                    cancellationToken);

                notification.Status = NotificationStatus.Sent;
                notification.SentAt = timeProvider.GetUtcNow();
                notification.LastError = null;
            }
            catch (Exception exception)
            {
                notification.Attempts++;
                notification.LastError = exception.Message[..Math.Min(exception.Message.Length, 900)];

                if (notification.Attempts >= Notification.MaxAttempts)
                {
                    notification.Status = NotificationStatus.Failed;

                    logger.LogError(
                        exception,
                        "Notification {Id} abandonnée après {Attempts} tentatives.",
                        notification.Id,
                        notification.Attempts);
                }
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // --- Balayages quotidiens ----------------------------------------------

    private async Task RunDailyScansAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var factory = Factory(scope);

        await using var root = factory.CreateFor(Guid.Empty);

        var agencies = await root.Agencies
            .IgnoreQueryFilters()
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        foreach (var agencyId in agencies)
        {
            await using var db = factory.CreateFor(agencyId);
            var queue = new NotificationQueue(db, NullLogger());

            var queued = 0;
            queued += await QueueMissionRemindersAsync(db, queue, agencyId, cancellationToken);
            queued += await QueueExpiringDocumentsAsync(db, queue, agencyId, cancellationToken);
            queued += await QueueInvoicesDueAsync(db, queue, agencyId, cancellationToken);

            if (queued > 0)
            {
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Balayage quotidien : {Count} notification(s) mise(s) en file pour l'agence {AgencyId}.",
                    queued,
                    agencyId);
            }
        }
    }

    /// <summary>Rappel la veille d'une mission confirmée.</summary>
    private async Task<int> QueueMissionRemindersAsync(
        LaGestionDbContext db,
        NotificationQueue queue,
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var horizon = now.AddDays(2);

        var assignments = await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Include(a => a.Contractor!)
            .ThenInclude(c => c.User)
            .Where(a => a.Status == AssignmentStatus.Confirmed)
            .Where(a => a.Position!.StartsAt > now && a.Position.StartsAt <= horizon)
            .ToListAsync(cancellationToken);

        var queued = 0;

        foreach (var assignment in assignments)
        {
            var position = assignment.Position!;

            var added = await queue.EnqueueOnceAsync(
                agencyId,
                assignment.Contractor!.User!,
                NotificationTemplates.MissionReminder,
                new Dictionary<string, string>
                {
                    ["positionLabel"] = position.Label,
                    ["eventTitle"] = position.Event!.Title,
                    ["when"] = Describe(position.StartsAt, position.EndsAt),
                    ["address"] = position.Event.Address ?? string.Empty,
                },
                $"reminder:assignment:{assignment.Id}",
                cancellationToken);

            queued += added ? 1 : 0;
        }

        return queued;
    }

    /// <summary>Pièces expirées ou expirant sous trente jours.</summary>
    private async Task<int> QueueExpiringDocumentsAsync(
        LaGestionDbContext db,
        NotificationQueue queue,
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var limit = today.AddDays(ExpiringSoonDays);

        var documents = await db.Documents
            .Include(d => d.Contractor!)
            .ThenInclude(c => c.User)
            .Where(d => d.Status == DocumentStatus.Approved)
            .Where(d => d.ExpiresAt != null && d.ExpiresAt <= limit)
            .ToListAsync(cancellationToken);

        var queued = 0;

        foreach (var document in documents)
        {
            var expired = document.IsExpired(today);

            // La clé porte le mois : une pièce qui reste périmée est
            // rappelée une fois par mois, pas tous les jours.
            var added = await queue.EnqueueOnceAsync(
                agencyId,
                document.Contractor!.User!,
                NotificationTemplates.DocumentExpiring,
                new Dictionary<string, string>
                {
                    ["documentType"] = DocumentLabels.For(document.Type),
                    ["expiresAt"] = document.ExpiresAt!.Value.ToString("dd/MM/yyyy"),
                    ["state"] = expired ? "expired" : "expiring",
                },
                $"document-expiry:{document.Id}:{today:yyyy-MM}",
                cancellationToken);

            queued += added ? 1 : 0;
        }

        return queued;
    }

    /// <summary>Prestations validées non facturées, une fois le mois échu.</summary>
    private async Task<int> QueueInvoicesDueAsync(
        LaGestionDbContext db,
        NotificationQueue queue,
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var periodStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var from = new DateTimeOffset(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(periodEnd.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var sheets = await db.Timesheets
            .Include(t => t.Assignment!)
            .ThenInclude(a => a.Position)
            .Include(t => t.Assignment!)
            .ThenInclude(a => a.Contractor!)
            .ThenInclude(c => c.User)
            .Where(t => t.Status == TimesheetStatus.Validated)
            .Where(t => t.Assignment!.Position!.StartsAt >= from && t.Assignment.Position.StartsAt <= to)
            .Where(t => !db.InvoiceLines.Any(l =>
                l.AssignmentId == t.AssignmentId && l.Invoice!.Status != InvoiceStatus.Cancelled))
            .ToListAsync(cancellationToken);

        var queued = 0;

        foreach (var group in sheets.GroupBy(t => t.Assignment!.ContractorId))
        {
            var user = group.First().Assignment!.Contractor!.User!;
            var total = group.Sum(t => t.ActualHours * t.Assignment!.Position!.HourlyRate);

            var added = await queue.EnqueueOnceAsync(
                agencyId,
                user,
                NotificationTemplates.InvoiceDue,
                new Dictionary<string, string>
                {
                    ["count"] = group.Count().ToString(),
                    ["total"] = $"{decimal.Round(total, 2)} €",
                },
                $"invoice-due:{group.Key}:{periodStart:yyyy-MM}",
                cancellationToken);

            queued += added ? 1 : 0;
        }

        return queued;
    }

    internal static string Describe(DateTimeOffset startsAt, DateTimeOffset endsAt) =>
        $"{startsAt.ToLocalTime():dddd d MMMM, HH'h'mm} – {endsAt.ToLocalTime():HH'h'mm}";

    private static ILogger<NotificationQueue> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationQueue>.Instance;
}

/// <summary>Libellés français des natures de pièces, pour les mails.</summary>
public static class DocumentLabels
{
    public static string For(DocumentType type) => type switch
    {
        DocumentType.IdentityCard => "pièce d'identité",
        DocumentType.UrssafCertificate => "attestation de vigilance URSSAF",
        DocumentType.CompanyRegistration => "Kbis ou avis SIRENE",
        DocumentType.LiabilityInsurance => "attestation RC pro",
        DocumentType.BankDetails => "RIB",
        DocumentType.DrivingLicence => "permis de conduire",
        DocumentType.Certification => "habilitation",
        _ => "pièce justificative",
    };
}
