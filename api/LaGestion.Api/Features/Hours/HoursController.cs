using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Hours;

/// <summary>Relevé d'heures d'une prestation.</summary>
/// <param name="Variance">
/// Écart entre déclaré et prévu, en heures. Négatif si la prestation a été
/// plus courte que prévu.
/// </param>
public sealed record TimesheetView(
    Guid Id,
    Guid AssignmentId,
    string EventTitle,
    string PositionLabel,
    string ContractorName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal HourlyRate,
    decimal PlannedHours,
    decimal ActualHours,
    decimal Variance,
    decimal Amount,
    string Status,
    string? ContractorNote,
    string? ReviewNote,
    DateTimeOffset? ValidatedAt);

/// <summary>Déclaration d'heures par le prestataire.</summary>
public sealed record DeclareHoursRequest(decimal ActualHours, string? Note);

/// <summary>Décision de l'agence : validation, éventuellement après correction.</summary>
/// <param name="ActualHours">
/// Heures retenues. Nul pour valider telles quelles les heures déclarées.
/// </param>
public sealed record ReviewHoursRequest(bool Validated, decimal? ActualHours, string? Note);

/// <summary>Saisie d'heures par l'agence sur une mission jamais déclarée.</summary>
public sealed record RecordHoursRequest(Guid AssignmentId, decimal ActualHours, string? Note);

/// <summary>Bornes de bon sens sur une déclaration.</summary>
file static class HoursBounds
{
    public const decimal Max = 24m;

    public static bool IsPlausible(decimal hours) => hours > 0 && hours <= Max;
}

[ApiController]
[Route("me/hours")]
[Authorize(Policy = "contractor")]
public sealed class MyHoursController(LaGestionDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Relevés du prestataire connecté.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TimesheetView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TimesheetView>>> List(CancellationToken cancellationToken)
    {
        var contractorId = await FindContractorIdAsync(cancellationToken);

        var sheets = await Query(db)
            .Where(t => t.Assignment!.ContractorId == contractorId)
            .OrderByDescending(t => t.Assignment!.Position!.StartsAt)
            .ToListAsync(cancellationToken);

        return Ok(sheets.Select(ToView).ToList());
    }

    /// <summary>
    /// Déclare les heures effectuées sur une mission confirmée et terminée.
    ///
    /// Redéclarer écrase la déclaration précédente tant que l'agence n'a pas
    /// validé : c'est une correction, pas un doublon.
    /// </summary>
    [HttpPost("{assignmentId:guid}")]
    [ProducesResponseType<TimesheetView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetView>> Declare(
        Guid assignmentId,
        DeclareHoursRequest request,
        CancellationToken cancellationToken)
    {
        if (!HoursBounds.IsPlausible(request.ActualHours))
        {
            ModelState.AddModelError(
                nameof(request.ActualHours),
                $"Indiquez un nombre d'heures entre 0 et {HoursBounds.Max}.");
            return ValidationProblem(ModelState);
        }

        var contractorId = await FindContractorIdAsync(cancellationToken);

        var assignment = await db.Assignments
            .Include(a => a.Position)
            .FirstOrDefaultAsync(
                a => a.Id == assignmentId && a.ContractorId == contractorId,
                cancellationToken);

        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.Status != AssignmentStatus.Confirmed)
        {
            return Problem(
                title: "Mission non confirmée",
                detail: "Seule une mission confirmée donne lieu à une déclaration d'heures.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = timeProvider.GetUtcNow();

        if (assignment.Position!.EndsAt > now)
        {
            return Problem(
                title: "Prestation non terminée",
                detail: "Vous pourrez déclarer vos heures une fois la prestation terminée.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var sheet = await db.Timesheets.FirstOrDefaultAsync(
            t => t.AssignmentId == assignmentId,
            cancellationToken);

        if (sheet is { Status: TimesheetStatus.Validated })
        {
            return Problem(
                title: "Heures déjà validées",
                detail: "L'agence a validé ces heures. Contactez-la pour toute correction.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (sheet is null)
        {
            sheet = new Timesheet
            {
                AssignmentId = assignmentId,
                PlannedHours = PlannedHours(assignment.Position),
            };

            db.Timesheets.Add(sheet);
        }

        sheet.ActualHours = request.ActualHours;
        sheet.ContractorNote = Normalise(request.Note);
        sheet.Status = TimesheetStatus.Submitted;
        sheet.ReviewNote = null;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToView(await ReloadAsync(db, sheet.Id, cancellationToken)));
    }

    private async Task<Guid?> FindContractorIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return await db.Contractors
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    internal static decimal PlannedHours(Position position) =>
        decimal.Round((decimal)(position.EndsAt - position.StartsAt).TotalHours, 2);

    internal static IQueryable<Timesheet> Query(LaGestionDbContext db) =>
        db.Timesheets
            .Include(t => t.Assignment!)
            .ThenInclude(a => a.Position!)
            .ThenInclude(p => p.Event)
            .Include(t => t.Assignment!)
            .ThenInclude(a => a.Contractor!)
            .ThenInclude(c => c.User);

    internal static Task<Timesheet> ReloadAsync(
        LaGestionDbContext db,
        Guid id,
        CancellationToken cancellationToken) =>
        Query(db).FirstAsync(t => t.Id == id, cancellationToken);

    internal static TimesheetView ToView(Timesheet sheet)
    {
        var position = sheet.Assignment!.Position!;
        var user = sheet.Assignment.Contractor!.User!;

        return new TimesheetView(
            sheet.Id,
            sheet.AssignmentId,
            position.Event!.Title,
            position.Label,
            $"{user.FirstName} {user.LastName}",
            position.StartsAt,
            position.EndsAt,
            position.HourlyRate,
            sheet.PlannedHours,
            sheet.ActualHours,
            sheet.Variance,
            decimal.Round(sheet.ActualHours * position.HourlyRate, 2),
            sheet.Status.ToString(),
            sheet.ContractorNote,
            sheet.ReviewNote,
            sheet.ValidatedAt);
    }

    internal static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
