namespace LaGestion.Api.Domain;

/// <summary>Les trois états du calendrier de disponibilités.</summary>
public enum AvailabilityStatus
{
    /// <summary>Le prestataire se déclare disponible.</summary>
    Available,

    /// <summary>Créneau occupé par une mission confirmée.</summary>
    Confirmed,

    /// <summary>Le prestataire se déclare indisponible.</summary>
    Unavailable,
}

/// <summary>
/// Disponibilité déclarée par le prestataire sur une date, éventuellement
/// restreinte à un créneau.
///
/// C'est une déclaration du prestataire, pas un horaire imposé par l'agence.
/// </summary>
public class Availability : AgencyOwnedEntity
{
    public Guid ContractorId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Début du créneau. Nul si la disponibilité couvre la journée.</summary>
    public TimeOnly? StartsAt { get; set; }

    /// <summary>Fin du créneau. Nul si la disponibilité couvre la journée.</summary>
    public TimeOnly? EndsAt { get; set; }

    public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Available;

    public Contractor? Contractor { get; set; }
}
