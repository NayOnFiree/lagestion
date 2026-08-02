namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Fournit l'agence du contexte courant, sur laquelle tous les accès aux
/// données sont filtrés.
///
/// La valeur ne vient <b>jamais</b> du client : ni corps de requête, ni query
/// string, ni en-tête. Elle est déduite côté serveur.
/// </summary>
public interface IAgencyContext
{
    /// <summary>Agence courante.</summary>
    Guid AgencyId { get; }
}

/// <summary>
/// Implémentation de développement : l'agence est lue dans la configuration
/// (<c>Tenant:DevAgencyId</c>), pas dans la requête.
///
/// Elle disparaîtra en phase 2 au profit d'une implémentation lisant le claim
/// d'agence du JWT. Seule l'implémentation change ; l'abstraction, le filtre
/// global et les entités restent identiques.
/// </summary>
public sealed class ConfigurationAgencyContext : IAgencyContext
{
    public const string ConfigurationKey = "Tenant:DevAgencyId";

    public ConfigurationAgencyContext(IConfiguration configuration)
    {
        var raw = configuration[ConfigurationKey];

        if (!Guid.TryParse(raw, out var agencyId))
        {
            throw new InvalidOperationException(
                $"La configuration « {ConfigurationKey} » est absente ou n'est pas un GUID valide. " +
                "Renseignez-la dans appsettings.Development.json.");
        }

        AgencyId = agencyId;
    }

    public Guid AgencyId { get; }
}
