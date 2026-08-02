using System.ComponentModel.DataAnnotations;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Paramètres de signature et de durée de vie des jetons.
///
/// <see cref="SigningKey"/> n'est jamais versionnée : user-secrets en local,
/// variable d'environnement <c>Jwt__SigningKey</c> ailleurs.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "lagestion";

    [Required]
    public string Audience { get; set; } = "lagestion";

    /// <summary>Clé HMAC, au moins 32 octets une fois décodée en UTF-8.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "La clé de signature doit faire au moins 32 caractères.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Durée de l'access token. Court volontairement : il n'est pas
    /// révocable, seule son expiration le neutralise.
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// Durée du refresh token. Il est révocable, stocké haché en base et
    /// remplacé à chaque utilisation.
    /// </summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 30;
}
