namespace LaGestion.Api.Domain;

/// <summary>Suivi d'une facture émise par un prestataire.</summary>
public enum InvoiceStatus
{
    /// <summary>Numérotée, PDF généré, pas encore transmise à l'agence.</summary>
    Issued,

    /// <summary>Déposée auprès de l'agence.</summary>
    Submitted,

    /// <summary>Validée par l'agence.</summary>
    Validated,

    /// <summary>Payée.</summary>
    Paid,

    /// <summary>
    /// Annulée. Jamais supprimée : son numéro reste consommé, sans quoi la
    /// séquence présenterait un trou.
    /// </summary>
    Cancelled,
}

/// <summary>
/// Facture émise par le prestataire. L'application pré-remplit et génère le
/// PDF ; elle n'émet pas à sa place et ne renumérote jamais.
///
/// La numérotation est propre à chaque prestataire, continue et sans trou :
/// <see cref="SequenceIndex"/> est attribué à la génération du PDF, pas
/// avant, et reste nul tant que la facture est un brouillon.
/// </summary>
public class Invoice : AgencyOwnedEntity
{
    public Guid ContractorId { get; set; }

    /// <summary>Numéro affiché, préfixe compris. Attribué à l'émission, jamais réécrit.</summary>
    public required string Number { get; set; }

    /// <summary>Rang dans la séquence du prestataire. Unique par prestataire.</summary>
    public int SequenceIndex { get; set; }

    // --- Mentions figées à l'émission --------------------------------------
    // Recopiées et non lues à l'affichage : un prestataire qui déménage ou
    // corrige son SIRET ne doit pas réécrire les factures déjà transmises.

    public required string IssuerName { get; set; }

    public string? IssuerAddress { get; set; }

    public string? IssuerSiret { get; set; }

    public required string ClientName { get; set; }

    public string? ClientAddress { get; set; }

    public string? ClientSiret { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Montant total, en euros.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Franchise en base de TVA, mention obligatoire sur le PDF.</summary>
    public bool VatExempt { get; set; } = true;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    /// <summary>Clé de stockage du PDF généré.</summary>
    public string? PdfKey { get; set; }

    public Contractor? Contractor { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = [];
}

/// <summary>Ligne de facture, en principe adossée à une prestation validée.</summary>
public class InvoiceLine : AgencyOwnedEntity
{
    public Guid InvoiceId { get; set; }

    /// <summary>Prestation facturée. Nul pour une ligne libre.</summary>
    public Guid? AssignmentId { get; set; }

    public required string Label { get; set; }

    public decimal Hours { get; set; }

    /// <summary>Tarif horaire appliqué, en euros.</summary>
    public decimal UnitRate { get; set; }

    public decimal Amount { get; set; }

    public Invoice? Invoice { get; set; }

    public Assignment? Assignment { get; set; }
}
