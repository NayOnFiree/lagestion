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

    Other,
}

/// <summary>Cycle de validation d'une pièce par l'agence.</summary>
public enum DocumentStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
}

/// <summary>
/// Pièce justificative déposée par un prestataire.
///
/// Le fichier lui-même vit hors base : seule sa clé de stockage est
/// persistée, l'accès se fait par URL signée à durée courte.
/// </summary>
public class Document : AgencyOwnedEntity
{
    public Guid ContractorId { get; set; }

    public DocumentType Type { get; set; }

    /// <summary>Clé de stockage : disque local en dev, S3-compatible en prod.</summary>
    public required string FileKey { get; set; }

    public DateOnly? IssuedAt { get; set; }

    public DateOnly? ExpiresAt { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public Contractor? Contractor { get; set; }

    public User? ReviewedBy { get; set; }
}
