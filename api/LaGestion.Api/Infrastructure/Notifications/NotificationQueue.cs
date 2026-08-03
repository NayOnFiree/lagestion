using System.Text.Json;
using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Infrastructure.Notifications;

/// <summary>
/// Mise en file des notifications.
///
/// Rien n'est envoyé dans le fil de la requête : une panne du serveur de mail
/// ne doit pas faire échouer une action métier qui, elle, a réussi. Le service
/// de fond dépile ensuite.
/// </summary>
public sealed class NotificationQueue(LaGestionDbContext db, ILogger<NotificationQueue> logger)
{
    /// <summary>
    /// Met un message en file. N'enregistre pas : l'appelant sauvegarde dans
    /// la même transaction que son action métier, pour qu'un message ne parte
    /// jamais sur une opération qui n'a pas abouti.
    /// </summary>
    /// <param name="dedupKey">
    /// Clé d'unicité facultative. Fournie, elle empêche le même message de
    /// repartir — indispensable pour les balayages périodiques.
    /// </param>
    public void Enqueue(
        Guid agencyId,
        User recipient,
        string template,
        object payload,
        string? dedupKey = null)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            logger.LogWarning(
                "Notification {Template} ignorée : le compte {UserId} n'a pas d'adresse.",
                template,
                recipient.Id);
            return;
        }

        db.Notifications.Add(new Notification
        {
            AgencyId = agencyId,
            UserId = recipient.Id,
            Channel = NotificationChannel.Email,
            Template = template,
            Payload = JsonSerializer.Serialize(payload),
            Recipient = recipient.Email,
            DedupKey = dedupKey,
        });
    }

    /// <summary>
    /// Met en file en ignorant les doublons déjà présents. Utilisé par les
    /// balayages quotidiens, qui repassent sur les mêmes données.
    /// </summary>
    public async Task<bool> EnqueueOnceAsync(
        Guid agencyId,
        User recipient,
        string template,
        object payload,
        string dedupKey,
        CancellationToken cancellationToken)
    {
        var exists = await db.Notifications
            .AnyAsync(n => n.DedupKey == dedupKey, cancellationToken);

        if (exists)
        {
            return false;
        }

        Enqueue(agencyId, recipient, template, payload, dedupKey);
        return true;
    }
}
