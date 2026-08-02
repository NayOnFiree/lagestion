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
    Pending,
    Sent,
    Failed,
}

/// <summary>Notification adressée à un compte, sur un canal donné.</summary>
public class Notification : AgencyOwnedEntity
{
    public Guid UserId { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>Identifiant du gabarit : mission proposée, rappel J-1, document expiré…</summary>
    public required string Template { get; set; }

    /// <summary>Variables du gabarit, stockées en jsonb.</summary>
    public string? Payload { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public DateTimeOffset? SentAt { get; set; }

    public User? User { get; set; }
}
