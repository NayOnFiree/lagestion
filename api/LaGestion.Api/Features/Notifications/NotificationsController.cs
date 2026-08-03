using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Notifications;

/// <summary>Ligne du journal des envois.</summary>
public sealed record NotificationEntry(
    Guid Id,
    string Template,
    string Channel,
    string Recipient,
    string RecipientName,
    string Status,
    int Attempts,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt);

[ApiController]
[Route("notifications")]
[Authorize(Policy = "admin")]
public sealed class NotificationsController(LaGestionDbContext db) : ControllerBase
{
    /// <summary>
    /// Journal des envois de l'agence.
    ///
    /// Sans lui, un mail qui ne part pas ne se voit pas : le canal est
    /// obligatoire, il doit être vérifiable.
    /// </summary>
    /// <param name="status">Filtre facultatif : Pending, Sent ou Failed.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationEntry>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = db.Notifications.Include(n => n.User).AsQueryable();

        if (Enum.TryParse<NotificationStatus>(status, out var wanted))
        {
            query = query.Where(n => n.Status == wanted);
        }

        var entries = await query
            // Les échecs et les envois bloqués remontent en tête.
            .OrderBy(n => n.Status == NotificationStatus.Sent ? 1 : 0)
            .ThenByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return Ok(entries.Select(n => new NotificationEntry(
            n.Id,
            n.Template,
            n.Channel.ToString(),
            n.Recipient,
            n.User is null ? string.Empty : $"{n.User.FirstName} {n.User.LastName}",
            n.Status.ToString(),
            n.Attempts,
            n.LastError,
            n.CreatedAt,
            n.SentAt)).ToList());
    }

    /// <summary>Remet en file un envoi abandonné, après correction de la cause.</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        if (notification.Status != NotificationStatus.Failed)
        {
            return Problem(
                title: "Envoi non abandonné",
                detail: "Seul un envoi en échec définitif se relance.",
                statusCode: StatusCodes.Status409Conflict);
        }

        notification.Status = NotificationStatus.Pending;
        notification.Attempts = 0;
        notification.LastError = null;

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
