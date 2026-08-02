namespace LaGestion.Api.Domain;

/// <summary>
/// Agence de staffing. Racine du multi-tenant : c'est la seule entité qui ne
/// porte pas d'<c>AgencyId</c>, puisqu'elle en est un.
/// </summary>
public class Agency : Entity
{
    public required string Name { get; set; }

    public string? Siret { get; set; }

    public string? Address { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }
}
