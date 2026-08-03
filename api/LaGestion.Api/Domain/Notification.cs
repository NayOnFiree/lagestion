namespace LaGestion.Api.Domain;

/// <summary>
/// Canal d'envoi. Le mail est le canal fiable et obligatoire ; le push web
/// est un confort, non garanti sur iOS. Rien de critique ne repose sur lui.
/// </summary>
public enum NotificationChannel
{
    Email,
    WebPush,
}

/// <summary>État d'acheminement d'une notification.</summary>
public enum NotificationStatus
{
    /// <summary>En file d'attente.</summary>
    Pending,

    Sent,

    /// <summary>Échec définitif, après épuisement des tentatives.</summary>
    Failed,
}

/// <summary>Notification adressée à un compte, sur un canal donné.</summary>
public class Notification : AgencyOwnedEntity
{
    /// <summary>Nombre de tentatives au-delà duquel on abandonne.</summary>
    public const int MaxAttempts = 5;

    public Guid UserId { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>Identifiant du gabarit : mission proposée, rappel J-1, document expiré…</summary>
    public required string Template { get; set; }

    /// <summary>Variables du gabarit, stockées en jsonb.</summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Destinataire figé à la mise en file : un changement d'adresse ne doit
    /// pas rerouter un message déjà prêt à partir.
    /// </summary>
    public required string Recipient { get; set; }

    /// <summary>
    /// Clé d'unicité, pour les envois périodiques. C'est elle qui empêche le
    /// balayage quotidien de renvoyer chaque jour le même rappel.
    /// </summary>
    public string? DedupKey { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public int Attempts { get; set; }

    /// <summary>Dernière erreur rencontrée, pour qu'un envoi bloqué soit lisible.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public User? User { get; set; }
}
