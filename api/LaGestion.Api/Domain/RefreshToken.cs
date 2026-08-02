namespace LaGestion.Api.Domain;

/// <summary>
/// Jeton de rafraîchissement, à usage unique.
///
/// Seul le condensat est stocké : une fuite de la table ne permet pas de
/// rejouer les jetons. Chaque utilisation en émet un nouveau et révoque le
/// précédent en le pointant vers son successeur — si un jeton déjà consommé
/// se représente, c'est qu'il a été volé, et toute la chaîne est révoquée.
/// </summary>
public class RefreshToken : AgencyOwnedEntity
{
    public Guid UserId { get; set; }

    /// <summary>Condensat SHA-256 du jeton, en hexadécimal.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Renseigné dès que le jeton est consommé ou invalidé.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Jeton émis en remplacement, pour reconstituer la chaîne.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public User? User { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
