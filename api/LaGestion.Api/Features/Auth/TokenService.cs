using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LaGestion.Api.Features.Auth;

/// <summary>Noms des claims propres à l'application.</summary>
public static class LaGestionClaims
{
    /// <summary>Agence du compte. C'est ce claim qui porte le filtre multi-tenant.</summary>
    public const string AgencyId = "agency_id";

    /// <summary>
    /// Rôle, en nom court. La correspondance automatique vers les URI
    /// WS-Federation est désactivée : les jetons restent lisibles.
    /// </summary>
    public const string Role = "role";
}

/// <summary>
/// Émission et rotation des jetons.
///
/// Toutes les opérations d'ici s'exécutent <b>avant</b> qu'une identité ne
/// soit établie : ni le filtre d'agence en lecture ni le contrôle d'agence en
/// écriture ne peuvent s'appliquer. Les lectures contournent donc le filtre
/// explicitement, et les écritures passent par un contexte lié à l'agence du
/// compte concerné.
/// </summary>
public sealed class TokenService(
    LaGestionDbContext db,
    AgencyDbContextFactory contextFactory,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>Access token signé, court, portant l'identité, le rôle et l'agence.</summary>
    public string CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var now = timeProvider.GetUtcNow();

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(LaGestionClaims.Role, user.Role.ToString()),
                new Claim(LaGestionClaims.AgencyId, user.AgencyId.ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Émet un refresh token. La valeur en clair n'est retournée qu'ici et
    /// n'est jamais persistée : la base ne contient que son condensat.
    /// </summary>
    public async Task<string> IssueRefreshTokenAsync(User user, CancellationToken cancellationToken)
    {
        var plainToken = NewToken();

        await using var scoped = contextFactory.CreateFor(user.AgencyId);

        scoped.RefreshTokens.Add(new RefreshToken
        {
            AgencyId = user.AgencyId,
            UserId = user.Id,
            TokenHash = Hash(plainToken),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(_options.RefreshTokenDays),
        });

        await scoped.SaveChangesAsync(cancellationToken);

        return plainToken;
    }

    /// <summary>
    /// Consomme un refresh token et en émet un nouveau.
    ///
    /// Si le jeton présenté a déjà été consommé, c'est qu'il a fuité : toute
    /// la chaîne active de l'utilisateur est révoquée et le rafraîchissement
    /// échoue.
    /// </summary>
    public async Task<(string PlainToken, User User)?> RotateAsync(
        string plainToken,
        CancellationToken cancellationToken)
    {
        var hash = Hash(plainToken);
        var now = timeProvider.GetUtcNow();

        var presented = await db.RefreshTokens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (presented is null)
        {
            return null;
        }

        // L'agence est maintenant connue : la suite se fait sur un contexte
        // qui lui est lié, contrôles d'écriture compris.
        await using var scoped = contextFactory.CreateFor(presented.AgencyId);

        var existing = await scoped.RefreshTokens.FirstAsync(t => t.Id == presented.Id, cancellationToken);

        if (existing.RevokedAt is not null)
        {
            // Rejeu d'un jeton déjà consommé : on considère la chaîne compromise.
            await scoped.RefreshTokens
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAt, now), cancellationToken);

            return null;
        }

        if (existing.ExpiresAt <= now)
        {
            return null;
        }

        var user = await scoped.Users.FirstOrDefaultAsync(
            u => u.Id == existing.UserId && u.IsActive,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var plainReplacement = NewToken();

        var replacement = new RefreshToken
        {
            AgencyId = user.AgencyId,
            UserId = user.Id,
            TokenHash = Hash(plainReplacement),
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
        };

        scoped.RefreshTokens.Add(replacement);

        existing.RevokedAt = now;
        existing.ReplacedByTokenId = replacement.Id;

        await scoped.SaveChangesAsync(cancellationToken);

        return (plainReplacement, user);
    }

    /// <summary>Révoque un jeton précis, à la déconnexion.</summary>
    public async Task RevokeAsync(string plainToken, CancellationToken cancellationToken)
    {
        var hash = Hash(plainToken);
        var now = timeProvider.GetUtcNow();

        await db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
