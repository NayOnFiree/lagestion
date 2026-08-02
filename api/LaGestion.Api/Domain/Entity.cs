namespace LaGestion.Api.Domain;

/// <summary>
/// Racine de toute entité persistée.
///
/// L'identifiant est un UUID v7 généré côté application : il est ordonné dans
/// le temps, ce qui garde les insertions en fin d'index au lieu de fragmenter
/// l'arbre comme le ferait un UUID v4.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Posé automatiquement à l'insertion par le DbContext.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Posé automatiquement à chaque écriture par le DbContext.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Marque une entité métier appartenant à une agence.
///
/// Toute entité portant cette interface se voit appliquer automatiquement,
/// par <c>LaGestionDbContext</c> :
/// <list type="bullet">
///   <item>un filtre de requête global sur <see cref="AgencyId"/> ;</item>
///   <item>l'affectation de <see cref="AgencyId"/> à l'insertion.</item>
/// </list>
/// Aucune entité métier ne doit exister en dehors d'une agence.
/// </summary>
public interface IAgencyOwned
{
    Guid AgencyId { get; set; }
}

/// <summary>Entité métier rattachée à une agence.</summary>
public abstract class AgencyOwnedEntity : Entity, IAgencyOwned
{
    public Guid AgencyId { get; set; }
}
