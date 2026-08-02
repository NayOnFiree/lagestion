namespace LaGestion.Api.Domain;

/// <summary>Nature d'une pièce du coffre à documents.</summary>
public enum DocumentType
{
    IdentityCard,

    /// <summary>Attestation de vigilance URSSAF.</summary>
    UrssafCertificate,

    /// <summary>Kbis ou avis de situation SIRENE.</summary>
    CompanyRegistration,

    /// <summary>Attestation de responsabilité civile professionnelle.</summary>
    LiabilityInsurance,

    BankDetails,

    /// <summary>Permis de conduire.</summary>
    DrivingLicence,

    /// <summary>Habilitation ou certification métier : CACES, SSIAP, électricité…</summary>
    Certification,

    Other,
}

/// <summary>Décision de l'agence sur une pièce.</summary>
public enum DocumentStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// Pièce justificative déposée par un prestataire.
///
/// Le fichier lui-même vit hors base : seule sa clé de stockage est
/// persistée, l'accès se fait par URL signée à durée courte.
///
/// L'expiration n'est pas un statut : elle se déduit de
/// <see cref="ExpiresAt"/> à la lecture. Un statut stocké supposerait un
/// traitement périodique pour le tenir à jour, et une pièce affichée comme
/// valide alors qu'elle est périmée serait pire que pas de statut du tout.
/// </summary>
public class Document : AgencyOwnedEntity
{
    public Guid ContractorId { get; set; }

    public DocumentType Type { get; set; }

    /// <summary>Clé de stockage : disque local en dev, S3-compatible en prod.</summary>
    public required string FileKey { get; set; }

    /// <summary>Nom du fichier tel que déposé, réaffiché au prestataire.</summary>
    public required string OriginalFileName { get; set; }

    /// <summary>Type MIME déduit des octets d'en-tête, pas de ce qu'annonce le client.</summary>
    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public DateOnly? IssuedAt { get; set; }

    public DateOnly? ExpiresAt { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>Motif du refus, indispensable pour éviter un redépôt à l'identique.</summary>
    public string? ReviewNote { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public Contractor? Contractor { get; set; }

    public User? ReviewedBy { get; set; }

    /// <summary>Vrai si la pièce porte une date de validité déjà dépassée.</summary>
    public bool IsExpired(DateOnly today) => ExpiresAt is { } expiry && expiry < today;

    /// <summary>Une pièce ne compte pour le dossier que validée et non périmée.</summary>
    public bool CountsTowardsCompleteness(DateOnly today) =>
        Status == DocumentStatus.Approved && !IsExpired(today);
}
