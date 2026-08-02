using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Ouvre un contexte de données lié à une agence <b>explicitement désignée</b>,
/// pour les rares traitements qui s'exécutent avant qu'une identité ne soit
/// établie — connexion, rafraîchissement de jeton — ou hors de toute requête
/// HTTP : seed, tâches de fond.
///
/// C'est volontairement la seule porte de sortie du filtre d'agence en
/// écriture, et elle oblige l'appelant à nommer l'agence sur laquelle il
/// travaille. Tout le reste du code passe par le <see cref="LaGestionDbContext"/>
/// injecté, dont l'agence vient du jeton.
/// </summary>
public sealed class AgencyDbContextFactory(DbContextOptions<LaGestionDbContext> options)
{
    public LaGestionDbContext CreateFor(Guid agencyId) => new(options, new FixedAgencyContext(agencyId));
}
