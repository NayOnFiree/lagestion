using LaGestion.Api.Domain;

namespace LaGestion.Api.Features.Network;

/// <summary>Détail d'un indicateur, pour qu'un score reste explicable.</summary>
/// <param name="Value">Valeur de 0 à 1, ou nul si l'indicateur n'a pas de données.</param>
public sealed record Indicator(decimal? Value, int Numerator, int Denominator);

/// <summary>Indicateurs d'un prestataire.</summary>
/// <param name="Score">
/// Note de 0 à 100, moyenne des seuls indicateurs disposant de données. Nul
/// pour un prestataire sans historique — un nouveau venu ne mérite ni bon ni
/// mauvais score.
/// </param>
public sealed record ContractorScore(
    int? Score,
    Indicator Acceptance,
    Indicator Reliability,
    decimal? AverageRating,
    int RatingCount,
    int CompletedMissions);

/// <summary>
/// Calcul des indicateurs, à la lecture.
///
/// Aucun score n'est stocké : il serait faux entre deux recalculs, et il
/// faudrait se souvenir de le rafraîchir après chaque événement qui l'affecte.
/// À l'échelle d'un réseau de quelques centaines de prestataires, recalculer
/// coûte moins cher que maintenir.
/// </summary>
public static class ScoreRules
{
    /// <param name="assignments">Toutes les propositions reçues par le prestataire.</param>
    /// <param name="cancelledEventIds">
    /// Événements annulés par l'agence. Les missions qu'ils portaient ne sont
    /// pas comptées comme des désistements : le prestataire n'y est pour rien.
    /// </param>
    /// <param name="ratings">Appréciations reçues.</param>
    public static ContractorScore Evaluate(
        IReadOnlyCollection<Assignment> assignments,
        IReadOnlySet<Guid> cancelledEventIds,
        IReadOnlyCollection<MissionRating> ratings,
        DateTimeOffset now)
    {
        var answered = assignments.Count(a => a.RespondedAt is not null);
        var accepted = assignments.Count(a =>
            a.RespondedAt is not null
            && a.Status is not AssignmentStatus.Declined);

        var acceptance = Ratio(accepted, answered);

        // Deux façons de faire défaut : se désister après s'être engagé, ou
        // ne jamais répondre et laisser le délai passer.
        var withdrawals = assignments.Count(a =>
            a.Status == AssignmentStatus.Cancelled
            && a.RespondedAt is not null
            && !cancelledEventIds.Contains(a.Position?.EventId ?? Guid.Empty));

        var ignored = assignments.Count(a =>
            a.Status == AssignmentStatus.Proposed
            && a.RespondedAt is null
            && a.ResponseDeadline is { } deadline
            && deadline < now);

        var honoured = assignments.Count - withdrawals - ignored;
        var reliability = Ratio(honoured, assignments.Count);

        var averageRating = ratings.Count == 0
            ? (decimal?)null
            : decimal.Round((decimal)ratings.Average(r => r.Rating), 2);

        var completed = assignments.Count(a =>
            a.Status == AssignmentStatus.Confirmed
            && a.Position is not null
            && a.Position.EndsAt < now);

        return new ContractorScore(
            Combine(acceptance.Value, reliability.Value, averageRating),
            acceptance,
            reliability,
            averageRating,
            ratings.Count,
            completed);
    }

    private static Indicator Ratio(int numerator, int denominator) =>
        denominator == 0
            ? new Indicator(null, numerator, denominator)
            : new Indicator(decimal.Round((decimal)numerator / denominator, 4), numerator, denominator);

    /// <summary>
    /// Moyenne des seuls indicateurs renseignés. Un prestataire sans
    /// appréciation n'est pas pénalisé pour cette absence : elle est ignorée
    /// plutôt que comptée comme un zéro.
    /// </summary>
    private static int? Combine(decimal? acceptance, decimal? reliability, decimal? averageRating)
    {
        var parts = new List<decimal>();

        if (acceptance is { } a)
        {
            parts.Add(a);
        }

        if (reliability is { } r)
        {
            parts.Add(r);
        }

        if (averageRating is { } rating)
        {
            // 1 à 5 ramené sur 0 à 1.
            parts.Add((rating - MissionRating.MinRating) / (MissionRating.MaxRating - MissionRating.MinRating));
        }

        return parts.Count == 0 ? null : (int)Math.Round(parts.Average() * 100, MidpointRounding.AwayFromZero);
    }
}
