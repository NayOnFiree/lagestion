using LaGestion.Api.Domain;

namespace LaGestion.Api.Features.Documents;

/// <summary>État de complétude d'un dossier prestataire.</summary>
/// <param name="IsComplete">Vrai si plus rien ne manque.</param>
/// <param name="SatisfiedCount">Nombre d'exigences satisfaites.</param>
/// <param name="TotalCount">Nombre total d'exigences.</param>
/// <param name="MissingDocumentTypes">Pièces obligatoires absentes, refusées ou périmées.</param>
/// <param name="MissingProfileFields">Champs du profil encore vides.</param>
public sealed record DossierCompleteness(
    bool IsComplete,
    int SatisfiedCount,
    int TotalCount,
    IReadOnlyList<string> MissingDocumentTypes,
    IReadOnlyList<string> MissingProfileFields);

/// <summary>
/// Règle de complétude du dossier, partagée par l'application prestataire et
/// le back-office : les deux côtés doivent afficher le même verdict.
/// </summary>
public static class DossierRules
{
    /// <summary>
    /// Pièces sans lesquelles l'agence ne peut pas justifier d'avoir vérifié
    /// son prestataire. Permis et habilitations restent facultatifs : ils ne
    /// concernent que certaines missions.
    /// </summary>
    public static readonly IReadOnlyList<DocumentType> RequiredDocuments =
    [
        DocumentType.IdentityCard,
        DocumentType.UrssafCertificate,
        DocumentType.CompanyRegistration,
        DocumentType.LiabilityInsurance,
    ];

    public static DossierCompleteness Evaluate(
        Contractor contractor,
        IEnumerable<Document> documents,
        DateOnly today)
    {
        var valid = documents
            .Where(d => d.CountsTowardsCompleteness(today))
            .Select(d => d.Type)
            .ToHashSet();

        var missingDocuments = RequiredDocuments
            .Where(type => !valid.Contains(type))
            .Select(type => type.ToString())
            .ToList();

        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(contractor.Siret))
        {
            missingFields.Add(nameof(Contractor.Siret));
        }

        if (string.IsNullOrWhiteSpace(contractor.Iban))
        {
            missingFields.Add(nameof(Contractor.Iban));
        }

        var total = RequiredDocuments.Count + 2;
        var satisfied = total - missingDocuments.Count - missingFields.Count;

        return new DossierCompleteness(
            missingDocuments.Count == 0 && missingFields.Count == 0,
            satisfied,
            total,
            missingDocuments,
            missingFields);
    }
}
