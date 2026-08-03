namespace LaGestion.Api.Domain;

/// <summary>Cycle de validation des heures d'une prestation.</summary>
public enum TimesheetStatus
{
    /// <summary>Heures déclarées, en attente de validation par l'agence.</summary>
    Submitted,

    /// <summary>Heures validées par l'agence, facturables.</summary>
    Validated,

    /// <summary>Écart contesté entre heures prévues et heures déclarées.</summary>
    Disputed,
}

/// <summary>
/// Heures d'une prestation : prévues au moment de la proposition, déclarées
/// après coup, puis validées par l'agence avant facturation.
///
/// Il n'y a pas de pointage. Le prestataire est indépendant : il déclare ce
/// qu'il a effectué, il ne badge pas. L'agence valide, corrige en cas d'oubli
/// ou conteste avec un motif.
///
/// Le relevé n'existe qu'à partir de la première déclaration : le créer à la
/// confirmation produirait des relevés orphelins sur les missions annulées.
/// </summary>
public class Timesheet : AgencyOwnedEntity
{
    public Guid AssignmentId { get; set; }

    /// <summary>Heures prévues, figées d'après le créneau du poste à la déclaration.</summary>
    public decimal PlannedHours { get; set; }

    /// <summary>Heures réellement effectuées, telles que déclarées puis éventuellement corrigées.</summary>
    public decimal ActualHours { get; set; }

    public TimesheetStatus Status { get; set; } = TimesheetStatus.Submitted;

    /// <summary>Commentaire du prestataire à la déclaration, s'il y a un écart à expliquer.</summary>
    public string? ContractorNote { get; set; }

    /// <summary>Motif de la contestation ou de la correction par l'agence.</summary>
    public string? ReviewNote { get; set; }

    public Guid? ValidatedByUserId { get; set; }

    public DateTimeOffset? ValidatedAt { get; set; }

    public Assignment? Assignment { get; set; }

    public User? ValidatedBy { get; set; }

    /// <summary>Écart entre heures déclarées et heures prévues. Négatif si la prestation a été plus courte.</summary>
    public decimal Variance => ActualHours - PlannedHours;
}
