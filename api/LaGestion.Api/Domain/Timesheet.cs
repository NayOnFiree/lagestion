namespace LaGestion.Api.Domain;

/// <summary>Cycle de validation des heures d'une prestation.</summary>
public enum TimesheetStatus
{
    /// <summary>Prestation à venir, aucun pointage.</summary>
    Pending,

    /// <summary>Heures pointées par le prestataire, en attente de validation.</summary>
    Submitted,

    /// <summary>Heures validées par l'agence, facturables.</summary>
    Validated,

    /// <summary>Écart contesté entre heures prévues et heures réelles.</summary>
    Disputed,
}

/// <summary>
/// Heures d'une prestation : prévues au moment de la proposition, réelles
/// après pointage, puis validées par l'agence avant facturation.
///
/// La géolocalisation n'est relevée qu'au check-in et au check-out, jamais en
/// continu.
/// </summary>
public class Timesheet : AgencyOwnedEntity
{
    public Guid AssignmentId { get; set; }

    /// <summary>Heures prévues, déduites du créneau du poste.</summary>
    public decimal PlannedHours { get; set; }

    public DateTimeOffset? CheckInAt { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    /// <summary>Heures réellement effectuées, renseignées après le check-out.</summary>
    public decimal? ActualHours { get; set; }

    public TimesheetStatus Status { get; set; } = TimesheetStatus.Pending;

    public Guid? ValidatedByUserId { get; set; }

    public DateTimeOffset? ValidatedAt { get; set; }

    public Assignment? Assignment { get; set; }

    public User? ValidatedBy { get; set; }
}
