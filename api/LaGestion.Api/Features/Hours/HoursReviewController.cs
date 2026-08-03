using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using LaGestion.Api.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Hours;

/// <summary>Mission terminée dont les heures n'ont pas encore été déclarées.</summary>
public sealed record MissingDeclaration(
    Guid AssignmentId,
    string EventTitle,
    string PositionLabel,
    string ContractorName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal PlannedHours,
    decimal HourlyRate);

[ApiController]
[Route("timesheets")]
[Authorize(Policy = "admin")]
public sealed class HoursReviewController(
    LaGestionDbContext db,
    TimeProvider timeProvider,
    NotificationQueue notifications) : ControllerBase
{
    /// <summary>Relevés d'heures de l'agence.</summary>
    /// <param name="status">Filtre facultatif : Submitted, Validated ou Disputed.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TimesheetView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TimesheetView>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = MyHoursController.Query(db);

        if (Enum.TryParse<TimesheetStatus>(status, out var wanted))
        {
            query = query.Where(t => t.Status == wanted);
        }

        var sheets = await query
            .OrderBy(t => t.Status == TimesheetStatus.Submitted ? 0 : 1)
            .ThenByDescending(t => t.Assignment!.Position!.StartsAt)
            .ToListAsync(cancellationToken);

        return Ok(sheets.Select(MyHoursController.ToView).ToList());
    }

    /// <summary>
    /// Missions confirmées et terminées dont les heures n'ont jamais été
    /// déclarées. Sans cette liste, un oubli reste invisible et la prestation
    /// n'est jamais facturée.
    /// </summary>
    [HttpGet("missing")]
    [ProducesResponseType<IReadOnlyList<MissingDeclaration>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MissingDeclaration>>> Missing(
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var assignments = await db.Assignments
            .Include(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Include(a => a.Contractor!)
            .ThenInclude(c => c.User)
            .Where(a => a.Status == AssignmentStatus.Confirmed)
            .Where(a => a.Position!.EndsAt < now)
            .Where(a => !db.Timesheets.Any(t => t.AssignmentId == a.Id))
            .OrderBy(a => a.Position!.StartsAt)
            .ToListAsync(cancellationToken);

        return Ok(assignments.Select(a => new MissingDeclaration(
            a.Id,
            a.Position!.Event!.Title,
            a.Position.Label,
            $"{a.Contractor!.User!.FirstName} {a.Contractor.User.LastName}",
            a.Position.StartsAt,
            a.Position.EndsAt,
            MyHoursController.PlannedHours(a.Position),
            a.Position.HourlyRate)).ToList());
    }

    /// <summary>
    /// Saisit les heures à la place du prestataire, quand il a oublié de les
    /// déclarer. Le relevé est directement validé : c'est l'agence qui écrit.
    /// </summary>
    [HttpPost("record")]
    [ProducesResponseType<TimesheetView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetView>> Record(
        RecordHoursRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ActualHours is <= 0 or > 24)
        {
            ModelState.AddModelError(nameof(request.ActualHours), "Indiquez un nombre d'heures entre 0 et 24.");
            return ValidationProblem(ModelState);
        }

        var assignment = await db.Assignments
            .Include(a => a.Position)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status != AssignmentStatus.Confirmed)
        {
            return Problem(
                title: "Mission non confirmée",
                detail: "Seule une mission confirmée donne lieu à un relevé d'heures.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await db.Timesheets.AnyAsync(t => t.AssignmentId == request.AssignmentId, cancellationToken))
        {
            return Problem(
                title: "Relevé déjà existant",
                detail: "Les heures de cette mission ont déjà été déclarées. Corrigez-les à la validation.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = timeProvider.GetUtcNow();

        var sheet = new Timesheet
        {
            AssignmentId = request.AssignmentId,
            PlannedHours = MyHoursController.PlannedHours(assignment.Position!),
            ActualHours = request.ActualHours,
            Status = TimesheetStatus.Validated,
            ReviewNote = MyHoursController.Normalise(request.Note) ?? "Heures saisies par l'agence.",
            ValidatedByUserId = CurrentUserId(),
            ValidatedAt = now,
        };

        db.Timesheets.Add(sheet);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(MyHoursController.ToView(await MyHoursController.ReloadAsync(db, sheet.Id, cancellationToken)));
    }

    /// <summary>
    /// Valide ou conteste des heures déclarées, éventuellement après
    /// correction.
    /// </summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType<TimesheetView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TimesheetView>> Review(
        Guid id,
        ReviewHoursRequest request,
        CancellationToken cancellationToken)
    {
        var note = MyHoursController.Normalise(request.Note);

        // Contester sans motif oblige le prestataire à deviner ; corriger ses
        // heures sans le dire est pire encore.
        if (!request.Validated && note is null)
        {
            ModelState.AddModelError(nameof(request.Note), "Indiquez le motif de la contestation.");
            return ValidationProblem(ModelState);
        }

        if (request.ActualHours is { } corrected)
        {
            if (corrected is <= 0 or > 24)
            {
                ModelState.AddModelError(nameof(request.ActualHours), "Indiquez un nombre d'heures entre 0 et 24.");
                return ValidationProblem(ModelState);
            }

            if (note is null)
            {
                ModelState.AddModelError(
                    nameof(request.Note),
                    "Une correction des heures déclarées doit être motivée.");
                return ValidationProblem(ModelState);
            }
        }

        var sheet = await MyHoursController.Query(db).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (sheet is null)
        {
            return NotFound();
        }

        if (request.ActualHours is { } hours)
        {
            sheet.ActualHours = hours;
        }

        if (!request.Validated)
        {
            var position = sheet.Assignment!.Position!;

            notifications.Enqueue(
                sheet.AgencyId,
                sheet.Assignment.Contractor!.User!,
                NotificationTemplates.HoursDisputed,
                new Dictionary<string, string>
                {
                    ["positionLabel"] = position.Label,
                    ["when"] = NotificationWorker.Describe(position.StartsAt, position.EndsAt),
                    ["reason"] = note!,
                });
        }

        sheet.ReviewNote = note;
        sheet.Status = request.Validated ? TimesheetStatus.Validated : TimesheetStatus.Disputed;
        sheet.ValidatedByUserId = request.Validated ? CurrentUserId() : null;
        sheet.ValidatedAt = request.Validated ? timeProvider.GetUtcNow() : null;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(MyHoursController.ToView(await MyHoursController.ReloadAsync(db, id, cancellationToken)));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
