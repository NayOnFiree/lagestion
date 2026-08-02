namespace LaGestion.Api.Domain;

/// <summary>
/// Ce que le prestataire déclare sur un créneau.
///
/// « Confirmé » n'en fait pas partie : ce n'est pas une déclaration mais la
/// conséquence d'une mission acceptée. Le calendrier le déduit des
/// <see cref="Assignment"/>, il n'est jamais stocké ici.
/// </summary>
public enum AvailabilityStatus
{
    /// <summary>Le prestataire se déclare disponible.</summary>
    Available,

    /// <summary>Le prestataire se déclare indisponible.</summary>
    Unavailable,
}

/// <summary>
/// Disponibilité déclarée par le prestataire sur une date, éventuellement
/// restreinte à un créneau.
///
/// C'est une déclaration du prestataire, pas un horaire imposé par l'agence.
/// Une récurrence saisie côté application se matérialise en autant de lignes
/// que de jours : chaque jour reste ensuite modifiable indépendamment.
/// </summary>
public class Availability : AgencyOwnedEntity
{
    public Guid ContractorId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Début du créneau. Nul si la déclaration couvre la journée.</summary>
    public TimeOnly? StartsAt { get; set; }

    /// <summary>Fin du créneau. Nul si la déclaration couvre la journée.</summary>
    public TimeOnly? EndsAt { get; set; }

    public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Available;

    public Contractor? Contractor { get; set; }

    /// <summary>Vrai si la déclaration couvre la journée entière.</summary>
    public bool IsWholeDay => StartsAt is null || EndsAt is null;

    /// <summary>
    /// Vrai si deux déclarations du même jour se recouvrent. Une déclaration
    /// sur la journée entière recouvre tout ce que porte cette date.
    /// </summary>
    public bool Overlaps(DateOnly date, TimeOnly? startsAt, TimeOnly? endsAt)
    {
        if (Date != date)
        {
            return false;
        }

        if (IsWholeDay || startsAt is null || endsAt is null)
        {
            return true;
        }

        return startsAt < EndsAt && StartsAt < endsAt;
    }
}
