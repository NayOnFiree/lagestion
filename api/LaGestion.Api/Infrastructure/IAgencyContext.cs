namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Fournit l'agence du contexte courant, sur laquelle tous les accès aux
/// données sont filtrés.
///
/// La valeur ne vient <b>jamais</b> du client : ni corps de requête, ni query
/// string, ni en-tête. Elle est déduite côté serveur, du claim signé porté par
/// le jeton d'accès.
/// </summary>
public interface IAgencyContext
{
    /// <summary>
    /// Agence courante. <see cref="Guid.Empty"/> quand aucune identité n'est
    /// établie : le filtre global ne laisse alors rien passer.
    /// </summary>
    Guid AgencyId { get; }
}

/// <summary>
/// Agence fixée à la construction, pour les traitements qui s'exécutent hors
/// requête HTTP : seed de développement, tâches de fond, tests.
///
/// N'est jamais enregistrée dans le conteneur : elle s'utilise en construisant
/// explicitement un <see cref="LaGestionDbContext"/>, ce qui rend visible dans
/// le code appelant sur quelle agence on travaille.
/// </summary>
public sealed class FixedAgencyContext(Guid agencyId) : IAgencyContext
{
    public Guid AgencyId { get; } = agencyId;
}
