namespace LaGestion.Api.Domain;

/// <summary>
/// Appréciation portée par l'agence sur une prestation effectuée.
///
/// Facultative : une mission peut très bien rester sans appréciation, et la
/// validation des heures n'en exige jamais une. Un prestataire sans retour
/// n'est pas pénalisé pour autant — le calcul du score ne pondère que les
/// indicateurs qui ont des données.
/// </summary>
public class MissionRating : AgencyOwnedEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public Guid AssignmentId { get; set; }

    /// <summary>Note de 1 à 5.</summary>
    public int Rating { get; set; }

    /// <summary>Commentaire libre, pour que la note reste explicable.</summary>
    public string? Comment { get; set; }

    public Guid RatedByUserId { get; set; }

    public DateTimeOffset RatedAt { get; set; }

    public Assignment? Assignment { get; set; }

    public User? RatedBy { get; set; }
}
