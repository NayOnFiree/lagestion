using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Health;

/// <summary>Réponse de l'endpoint de santé.</summary>
/// <param name="Status">"healthy" si tout répond, "degraded" sinon.</param>
/// <param name="Database">Vrai si la connexion PostgreSQL est établie.</param>
/// <param name="Timestamp">Instant de la vérification (UTC).</param>
public sealed record HealthResponse(string Status, bool Database, DateTimeOffset Timestamp);

[ApiController]
[Route("health")]
public sealed class HealthController(LaGestionDbContext db) : ControllerBase
{
    /// <summary>Vérifie que l'API répond et que la base est joignable.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var databaseReachable = await db.Database.CanConnectAsync(cancellationToken);

        var response = new HealthResponse(
            databaseReachable ? "healthy" : "degraded",
            databaseReachable,
            DateTimeOffset.UtcNow);

        return databaseReachable
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
