using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LaGestion.Api.Features.Auth;

/// <param name="AgencySlug">Identifiant court de l'agence, saisi à la connexion.</param>
public sealed record LoginRequest(string AgencySlug, string Email, string Password);

/// <summary>Compte connecté, tel que les fronts l'affichent.</summary>
public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid AgencyId,
    string AgencyName);

/// <param name="AccessToken">À garder en mémoire, jamais dans le stockage local.</param>
/// <param name="ExpiresInSeconds">Durée de validité de l'access token.</param>
public sealed record AuthResponse(string AccessToken, int ExpiresInSeconds, AuthenticatedUser User);

[ApiController]
[Route("auth")]
public sealed class AuthController(
    LaGestionDbContext db,
    TokenService tokens,
    AgencyDbContextFactory contextFactory,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Le refresh token voyage en cookie httpOnly : inaccessible au
    /// JavaScript, donc hors de portée d'une injection de script.
    /// </summary>
    private const string RefreshCookieName = "lagestion_refresh";

    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <summary>Ouvre une session à partir d'une agence, d'un email et d'un mot de passe.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        // Aucune identité n'est encore établie : le filtre d'agence ne peut pas
        // s'appliquer, c'est le slug saisi qui désigne l'agence.
        var slug = request.AgencySlug.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        var agency = await db.Agencies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);

        var user = agency is null
            ? null
            : await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.AgencyId == agency.Id && u.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // Même coût qu'une vérification réelle : sans cela, le temps de
            // réponse révélerait quels comptes existent.
            passwordHasher.VerifyHashedPassword(PlaceholderUser, DummyHash, request.Password);

            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Le format de hachage a durci depuis la création du compte.
            // L'écriture passe par un contexte lié à l'agence du compte :
            // à ce stade aucune identité n'est encore établie.
            await using var scoped = contextFactory.CreateFor(user.AgencyId);
            var tracked = await scoped.Users.FirstAsync(u => u.Id == user.Id, cancellationToken);
            tracked.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await scoped.SaveChangesAsync(cancellationToken);
        }

        var refreshToken = await tokens.IssueRefreshTokenAsync(user, cancellationToken);
        SetRefreshCookie(refreshToken);

        logger.LogInformation("Connexion de {UserId} sur l'agence {AgencyId}.", user.Id, user.AgencyId);

        return Ok(BuildResponse(user, agency!));
    }

    /// <summary>Échange le refresh token du cookie contre un nouvel access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var presented) || string.IsNullOrEmpty(presented))
        {
            return InvalidCredentials();
        }

        var rotation = await tokens.RotateAsync(presented, cancellationToken);

        if (rotation is null)
        {
            ClearRefreshCookie();
            return InvalidCredentials();
        }

        var (replacement, user) = rotation.Value;
        SetRefreshCookie(replacement);

        var agency = await db.Agencies
            .IgnoreQueryFilters()
            .FirstAsync(a => a.Id == user.AgencyId, cancellationToken);

        return Ok(BuildResponse(user, agency));
    }

    /// <summary>Révoque le refresh token courant et efface le cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var presented) && !string.IsNullOrEmpty(presented))
        {
            await tokens.RevokeAsync(presented, cancellationToken);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Compte associé à l'access token présenté.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUser>> Me(CancellationToken cancellationToken)
    {
        // La traduction des claims vers les URI WS-Federation est désactivée :
        // l'identifiant reste sous son nom standard, « sub ».
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Unauthorized();
        }

        // Le filtre d'agence s'applique : inutile de le contourner ici.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var agency = await db.Agencies.FirstAsync(a => a.Id == user.AgencyId, cancellationToken);

        return Ok(ToAuthenticatedUser(user, agency));
    }

    private AuthResponse BuildResponse(User user, Agency agency) => new(
        tokens.CreateAccessToken(user),
        _jwt.AccessTokenMinutes * 60,
        ToAuthenticatedUser(user, agency));

    private static AuthenticatedUser ToAuthenticatedUser(User user, Agency agency) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Role.ToString(),
        user.AgencyId,
        agency.Name);

    /// <summary>
    /// Réponse unique quel que soit le motif : agence inconnue, compte
    /// inexistant, compte désactivé ou mot de passe faux. Rien ne doit
    /// permettre d'énumérer les comptes.
    /// </summary>
    private ActionResult InvalidCredentials() => Problem(
        title: "Identifiants invalides",
        detail: "Agence, adresse électronique ou mot de passe incorrect.",
        statusCode: StatusCodes.Status401Unauthorized);

    private void SetRefreshCookie(string token) =>
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            MaxAge = TimeSpan.FromDays(_jwt.RefreshTokenDays),
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
        });

    private static readonly User PlaceholderUser = new()
    {
        Email = string.Empty,
        PasswordHash = string.Empty,
        FirstName = string.Empty,
        LastName = string.Empty,
    };

    /// <summary>
    /// Condensat jetable, uniquement là pour égaliser les temps de réponse
    /// quand aucun compte ne correspond. Calculé une fois au chargement : un
    /// condensat écrit en dur risquerait d'être mal formé, et sa vérification
    /// lèverait au lieu de coûter le même temps qu'une vérification réelle.
    /// </summary>
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(PlaceholderUser, "aucun-compte-correspondant");
}
