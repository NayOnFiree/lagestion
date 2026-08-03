namespace LaGestion.Api.Domain;

/// <summary>
/// Statut juridique sous lequel le prestataire facture. C'est une information
/// administrative portée par la fiche, pas un rôle applicatif.
/// </summary>
public enum LegalStatus
{
    AutoEntrepreneur,
    EntrepriseIndividuelle,
    Eurl,
    Sasu,
    Sarl,
    Other,
}

/// <summary>
/// Prestataire indépendant. « Prestataire » dans l'UI, jamais « road ».
///
/// La relation est commerciale : tarif horaire, prestations, disponibilités
/// déclarées. Rien de ce modèle ne décrit un lien de subordination.
/// </summary>
public class Contractor : AgencyOwnedEntity
{
    public Guid UserId { get; set; }

    public LegalStatus LegalStatus { get; set; }

    public string? Siret { get; set; }

    public string? Address { get; set; }

    public string? Iban { get; set; }

    /// <summary>Tarif horaire par défaut, en euros, hors taxes.</summary>
    public decimal? DefaultHourlyRate { get; set; }

    /// <summary>Ville de rattachement, base des recherches par zone.</summary>
    public string? BaseCity { get; set; }

    /// <summary>Rayon de déplacement accepté, en kilomètres.</summary>
    public int? TravelRadiusKm { get; set; }

    /// <summary>Score de fiabilité, alimenté en phase 10. Nul tant qu'aucun retour n'existe.</summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Préfixe de numérotation, tel que le prestataire l'utilise déjà.
    /// Exemple : « F2026- ».
    /// </summary>
    public string? InvoicePrefix { get; set; }

    /// <summary>
    /// Rang de la prochaine facture. Démarre à 1, mais se règle : un
    /// prestataire qui a déjà facturé jusqu'à 41 ailleurs reprend à 42, sans
    /// quoi il émettrait une seconde facture n° 1.
    /// </summary>
    public int NextInvoiceSequence { get; set; } = 1;

    public string? Notes { get; set; }

    public User? User { get; set; }

    public ICollection<ContractorSkill> Skills { get; set; } = [];

    public ICollection<Document> Documents { get; set; } = [];
}
