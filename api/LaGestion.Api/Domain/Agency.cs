namespace LaGestion.Api.Domain;

/// <summary>
/// Agence de staffing. Racine du multi-tenant : c'est la seule entité qui ne
/// porte pas d'<c>AgencyId</c>, puisqu'elle en est un.
/// </summary>
public class Agency : Entity
{
    public required string Name { get; set; }

    /// <summary>
    /// Identifiant court saisi à la connexion, unique toutes agences
    /// confondues : c'est lui qui désigne l'agence avant qu'aucune identité ne
    /// soit établie. En minuscules, sans espace.
    /// </summary>
    public required string Slug { get; set; }

    public string? Siret { get; set; }

    public string? Address { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }
}
