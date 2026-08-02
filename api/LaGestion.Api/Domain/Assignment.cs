namespace LaGestion.Api.Domain;

/// <summary>Cycle de vie d'une proposition de mission.</summary>
public enum AssignmentStatus
{
    /// <summary>Proposée au prestataire, en attente de sa réponse.</summary>
    Proposed,

    /// <summary>Acceptée par le prestataire.</summary>
    Accepted,

    /// <summary>Refusée par le prestataire.</summary>
    Declined,

    /// <summary>Confirmée par l'agence après acceptation.</summary>
    Confirmed,

    /// <summary>Annulée. L'enregistrement est conservé pour la traçabilité.</summary>
    Cancelled,
}

/// <summary>
/// Proposition de mission faite à un prestataire sur un poste.
///
/// Le prestataire accepte ou refuse : c'est une proposition commerciale avec
/// une date limite de réponse, jamais une affectation imposée.
/// </summary>
public class Assignment : AgencyOwnedEntity
{
    public Guid PositionId { get; set; }

    public Guid ContractorId { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Proposed;

    public DateTimeOffset ProposedAt { get; set; }

    /// <summary>Date limite de réponse laissée au prestataire.</summary>
    public DateTimeOffset? ResponseDeadline { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

    public Position? Position { get; set; }

    public Contractor? Contractor { get; set; }

    public Timesheet? Timesheet { get; set; }
}
