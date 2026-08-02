namespace LaGestion.Api.Domain;

/// <summary>
/// Poste à pourvoir sur un événement : un intitulé, un nombre de prestataires
/// recherchés, un créneau et un tarif horaire.
/// </summary>
public class Position : AgencyOwnedEntity
{
    public Guid EventId { get; set; }

    public required string Label { get; set; }

    /// <summary>Nombre de prestataires recherchés sur ce poste.</summary>
    public int Headcount { get; set; } = 1;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    /// <summary>Tarif horaire proposé, en euros, hors taxes.</summary>
    public decimal HourlyRate { get; set; }

    public string? DressCode { get; set; }

    public string? Brief { get; set; }

    public Event? Event { get; set; }

    public ICollection<Assignment> Assignments { get; set; } = [];
}
