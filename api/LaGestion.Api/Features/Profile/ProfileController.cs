using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LaGestion.Api.Domain;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Features.Profile;

/// <summary>Fiche du prestataire connecté.</summary>
public sealed record ContractorProfile(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string LegalStatus,
    string? Siret,
    string? Address,
    string? Iban,
    decimal? DefaultHourlyRate,
    string? BaseCity,
    int? TravelRadiusKm);

/// <summary>
/// Champs modifiables par le prestataire. Ni le rôle, ni l'agence, ni le
/// score ne s'y trouvent : ce sont des données de l'agence, pas du compte.
/// </summary>
public sealed record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string LegalStatus,
    string? Siret,
    string? Address,
    string? Iban,
    decimal? DefaultHourlyRate,
    string? BaseCity,
    int? TravelRadiusKm);

[ApiController]
[Route("me/profile")]
[Authorize(Policy = "contractor")]
public sealed class ProfileController(LaGestionDbContext db) : ControllerBase
{
    /// <summary>Fiche du prestataire connecté.</summary>
    [HttpGet]
    [ProducesResponseType<ContractorProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractorProfile>> Get(CancellationToken cancellationToken)
    {
        var contractor = await LoadAsync(cancellationToken);

        return contractor is null
            ? NoContractorFile()
            : Ok(ToProfile(contractor));
    }

    /// <summary>Met à jour la fiche du prestataire connecté.</summary>
    [HttpPut]
    [ProducesResponseType<ContractorProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractorProfile>> Update(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LegalStatus>(request.LegalStatus, out var legalStatus))
        {
            ModelState.AddModelError(nameof(request.LegalStatus), "Statut juridique inconnu.");
            return ValidationProblem(ModelState);
        }

        var siret = Normalise(request.Siret);

        if (siret is not null && (siret.Length != 14 || !siret.All(char.IsAsciiDigit)))
        {
            ModelState.AddModelError(nameof(request.Siret), "Le SIRET compte 14 chiffres.");
            return ValidationProblem(ModelState);
        }

        var iban = Normalise(request.Iban)?.Replace(" ", string.Empty).ToUpperInvariant();

        if (iban is not null && !IsPlausibleIban(iban))
        {
            ModelState.AddModelError(nameof(request.Iban), "IBAN invalide.");
            return ValidationProblem(ModelState);
        }

        if (request.DefaultHourlyRate is < 0)
        {
            ModelState.AddModelError(nameof(request.DefaultHourlyRate), "Le tarif horaire ne peut pas être négatif.");
            return ValidationProblem(ModelState);
        }

        var contractor = await LoadAsync(cancellationToken);

        if (contractor is null)
        {
            return NoContractorFile();
        }

        contractor.User!.FirstName = request.FirstName.Trim();
        contractor.User.LastName = request.LastName.Trim();
        contractor.User.Phone = Normalise(request.Phone);

        contractor.LegalStatus = legalStatus;
        contractor.Siret = siret;
        contractor.Address = Normalise(request.Address);
        contractor.Iban = iban;
        contractor.DefaultHourlyRate = request.DefaultHourlyRate;
        contractor.BaseCity = Normalise(request.BaseCity);
        contractor.TravelRadiusKm = request.TravelRadiusKm;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToProfile(contractor));
    }

    /// <summary>
    /// Charge la fiche du compte connecté. Le filtre d'agence s'applique, et
    /// la recherche part de l'identifiant du jeton : un prestataire ne peut
    /// atteindre que sa propre fiche.
    /// </summary>
    private async Task<Contractor?> LoadAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return await db.Contractors
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    private ActionResult NoContractorFile() => Problem(
        title: "Fiche prestataire introuvable",
        detail: "Ce compte n'est rattaché à aucune fiche prestataire.",
        statusCode: StatusCodes.Status404NotFound);

    private static ContractorProfile ToProfile(Contractor contractor) => new(
        contractor.Id,
        contractor.User!.FirstName,
        contractor.User.LastName,
        contractor.User.Email,
        contractor.User.Phone,
        contractor.LegalStatus.ToString(),
        contractor.Siret,
        contractor.Address,
        contractor.Iban,
        contractor.DefaultHourlyRate,
        contractor.BaseCity,
        contractor.TravelRadiusKm);

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Contrôle de forme et clé de contrôle modulo 97 (ISO 13616). Ne vérifie
    /// évidemment pas que le compte existe.
    /// </summary>
    private static bool IsPlausibleIban(string iban)
    {
        if (iban.Length is < 15 or > 34 || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;

        foreach (var character in rearranged)
        {
            var value = char.IsAsciiDigit(character)
                ? character - '0'
                : character - 'A' + 10;

            remainder = value > 9
                ? (remainder * 100 + value) % 97
                : (remainder * 10 + value) % 97;
        }

        return remainder == 1;
    }
}
