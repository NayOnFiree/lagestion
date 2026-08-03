using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Staffing;

/// <summary>
/// Règles partagées par le back-office et l'application prestataire : les
/// deux côtés doivent trancher pareil, sans quoi l'un propose ce que l'autre
/// refuse.
/// </summary>
public static class StaffingRules
{
    /// <summary>Statuts qui occupent une place sur un poste.</summary>
    public static bool OccupiesSlot(AssignmentStatus status) =>
        status is AssignmentStatus.Confirmed;

    /// <summary>Statuts encore en jeu : ni refusés, ni annulés.</summary>
    public static bool IsLive(AssignmentStatus status) =>
        status is AssignmentStatus.Proposed or AssignmentStatus.Accepted or AssignmentStatus.Confirmed;

    /// <summary>
    /// Une proposition dont la date limite est passée sans réponse. Calculée
    /// à la lecture : sans ordonnanceur, un statut stocké mentirait jusqu'au
    /// prochain passage.
    /// </summary>
    public static bool IsExpired(Assignment assignment, DateTimeOffset now) =>
        assignment.Status == AssignmentStatus.Proposed
        && assignment.ResponseDeadline is { } deadline
        && deadline < now;

    /// <summary>
    /// Vrai si le prestataire a déjà une mission <b>confirmée</b> qui
    /// chevauche ce créneau.
    ///
    /// Seules les missions confirmées bloquent : une proposition acceptée
    /// n'est qu'une candidature tant que l'agence n'a pas tranché, et un
    /// prestataire a le droit de se porter candidat à deux endroits.
    /// </summary>
    public static Task<bool> HasConfirmedConflictAsync(
        LaGestionDbContext db,
        Guid contractorId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        Guid? exceptAssignmentId,
        CancellationToken cancellationToken) =>
        db.Assignments
            .Where(a => a.ContractorId == contractorId)
            .Where(a => a.Status == AssignmentStatus.Confirmed)
            .Where(a => exceptAssignmentId == null || a.Id != exceptAssignmentId)
            .AnyAsync(a => a.Position!.StartsAt < endsAt && a.Position.EndsAt > startsAt, cancellationToken);

    /// <summary>Places déjà occupées sur un poste.</summary>
    public static Task<int> CountOccupiedAsync(
        LaGestionDbContext db,
        Guid positionId,
        CancellationToken cancellationToken) =>
        db.Assignments
            .Where(a => a.PositionId == positionId && a.Status == AssignmentStatus.Confirmed)
            .CountAsync(cancellationToken);

    /// <summary>
    /// Vrai si le prestataire s'est déclaré disponible sur tout le créneau et
    /// ne s'est déclaré indisponible sur aucune partie.
    ///
    /// Une déclaration sur la journée entière couvre le créneau ; une
    /// déclaration horaire doit l'englober.
    /// </summary>
    public static bool CoversSlot(
        IReadOnlyCollection<Availability> declarations,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        var days = DaysBetween(startsAt, endsAt);

        if (declarations.Any(d => days.Contains(d.Date) && d.Status == AvailabilityStatus.Unavailable))
        {
            return false;
        }

        // Chaque journée touchée par le créneau doit être couverte.
        return days.All(day => declarations.Any(d =>
            d.Date == day
            && d.Status == AvailabilityStatus.Available
            && (d.IsWholeDay || CoversTimeRange(d, day, startsAt, endsAt))));
    }

    private static bool CoversTimeRange(
        Availability declaration,
        DateOnly day,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        var dayStart = new DateTimeOffset(day.ToDateTime(declaration.StartsAt!.Value), TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(day.ToDateTime(declaration.EndsAt!.Value), TimeSpan.Zero);

        return dayStart <= startsAt && dayEnd >= endsAt;
    }

    private static List<DateOnly> DaysBetween(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        var first = DateOnly.FromDateTime(startsAt.UtcDateTime);
        var last = DateOnly.FromDateTime(endsAt.UtcDateTime);
        var days = new List<DateOnly>();

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            days.Add(day);
        }

        return days;
    }
}
