namespace LaGestion.Api.Domain;

/// <summary>Cycle de vie d'un événement.</summary>
public enum EventStatus
{
    /// <summary>En préparation. Invisible des prestataires.</summary>
    Draft,

    /// <summary>Ouvert au staffing.</summary>
    Published,

    /// <summary>
    /// Annulé. L'événement n'est jamais supprimé : il a existé, des
    /// prestataires ont pu être sollicités, et l'historique doit le refléter.
    /// </summary>
    Cancelled,
}

/// <summary>Événement pour lequel l'agence staffe des prestataires.</summary>
public class Event : AgencyOwnedEntity
{
    /// <summary>Client final. Masqué côté prestataire si l'événement est confidentiel.</summary>
    public string? ClientName { get; set; }

    public required string Title { get; set; }

    public string? Address { get; set; }

    /// <summary>Consignes d'accès au site, transmises aux prestataires retenus.</summary>
    public string? AccessNotes { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    /// <summary>Si vrai, le nom du client n'est pas exposé côté prestataire.</summary>
    public bool IsConfidential { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTimeOffset? CancelledAt { get; set; }

    public ICollection<Position> Positions { get; set; } = [];
}
