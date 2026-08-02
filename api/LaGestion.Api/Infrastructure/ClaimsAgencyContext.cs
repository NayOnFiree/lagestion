using LaGestion.Api.Features.Auth;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Agence courante lue dans le claim du jeton d'accès.
///
/// C'est le serveur qui l'a mise là en signant le jeton : elle ne peut pas
/// être forgée par le client sans invalider la signature.
///
/// Sur une requête anonyme, il n'y a pas d'agence : <see cref="AgencyId"/>
/// vaut alors <see cref="Guid.Empty"/> et le filtre global ne laisse rien
/// passer. Les rares chemins qui doivent lire hors agence — la connexion, le
/// rafraîchissement — le font explicitement en <c>IgnoreQueryFilters()</c>.
/// </summary>
public sealed class ClaimsAgencyContext(IHttpContextAccessor accessor) : IAgencyContext
{
    public Guid AgencyId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirst(LaGestionClaims.AgencyId)?.Value;

            return Guid.TryParse(raw, out var agencyId) ? agencyId : Guid.Empty;
        }
    }
}
