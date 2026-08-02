using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Jeu de données minimal pour travailler en local : une agence, un compte
/// d'agence, trois prestataires, un événement et ses deux postes.
///
/// Idempotent, comme <c>scripts/init-db.sql</c> : si l'agence de dev est déjà
/// là, le seed ne fait rien. Les identifiants sont fixes, pour que les URLs
/// et les captures d'écran restent stables d'une remise à zéro à l'autre.
///
/// Ne s'exécute qu'en environnement de développement.
/// </summary>
public sealed class DevelopmentSeeder(LaGestionDbContext db, IAgencyContext agencyContext, ILogger<DevelopmentSeeder> logger)
{
    /// <summary>
    /// Aucun compte du seed n'est connectable : la valeur stockée n'est le
    /// condensat d'aucun mot de passe, aucune vérification ne peut aboutir.
    /// Le vrai schéma de hachage arrive avec l'authentification, en phase 2.
    /// </summary>
    private const string UnusablePasswordHash = "!seed-aucun-mot-de-passe-connectable";

    private static readonly Guid AdminUserId = new("0198f000-0000-7000-8000-000000000010");

    private static readonly (Guid UserId, Guid ContractorId, string FirstName, string LastName, string Email, decimal Rate)[] Contractors =
    [
        (new("0198f000-0000-7000-8000-000000000011"), new("0198f000-0000-7000-8000-000000000021"), "Camille", "Rousseau", "camille.rousseau@example.test", 18.00m),
        (new("0198f000-0000-7000-8000-000000000012"), new("0198f000-0000-7000-8000-000000000022"), "Yanis", "Belkacem", "yanis.belkacem@example.test", 20.00m),
        (new("0198f000-0000-7000-8000-000000000013"), new("0198f000-0000-7000-8000-000000000023"), "Léa", "Marchand", "lea.marchand@example.test", 22.50m),
    ];

    private static readonly Guid EventId = new("0198f000-0000-7000-8000-000000000030");
    private static readonly Guid BarPositionId = new("0198f000-0000-7000-8000-000000000031");
    private static readonly Guid WelcomePositionId = new("0198f000-0000-7000-8000-000000000032");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var agencyId = agencyContext.AgencyId;

        if (await db.Agencies.AnyAsync(a => a.Id == agencyId, cancellationToken))
        {
            logger.LogInformation("Seed de dev : agence {AgencyId} déjà présente, rien à faire.", agencyId);
            return;
        }

        db.Agencies.Add(new Agency
        {
            Id = agencyId,
            Name = "Agence de démonstration",
            Siret = "12345678900012",
            Address = "12 rue des Fêtes, 44000 Nantes",
            ContactEmail = "contact@agence-demo.test",
            ContactPhone = "0240000000",
        });

        db.Users.Add(new User
        {
            Id = AdminUserId,
            AgencyId = agencyId,
            Email = "admin@agence-demo.test",
            PasswordHash = UnusablePasswordHash,
            Role = UserRole.Admin,
            FirstName = "Sacha",
            LastName = "Dumont",
            Phone = "0600000000",
        });

        foreach (var (userId, contractorId, firstName, lastName, email, rate) in Contractors)
        {
            db.Users.Add(new User
            {
                Id = userId,
                AgencyId = agencyId,
                Email = email,
                PasswordHash = UnusablePasswordHash,
                Role = UserRole.Contractor,
                FirstName = firstName,
                LastName = lastName,
            });

            db.Contractors.Add(new Contractor
            {
                Id = contractorId,
                AgencyId = agencyId,
                UserId = userId,
                LegalStatus = LegalStatus.AutoEntrepreneur,
                DefaultHourlyRate = rate,
                BaseCity = "Nantes",
                TravelRadiusKm = 50,
            });
        }

        var startsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(14).AddHours(18), TimeSpan.Zero);

        db.Events.Add(new Event
        {
            Id = EventId,
            AgencyId = agencyId,
            Title = "Soirée de lancement",
            ClientName = "Client de démonstration",
            Address = "Quai des Antilles, 44200 Nantes",
            AccessNotes = "Entrée personnel côté est, badge à retirer à l'accueil.",
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(6),
        });

        db.Positions.AddRange(
            new Position
            {
                Id = BarPositionId,
                AgencyId = agencyId,
                EventId = EventId,
                Label = "Barman",
                Headcount = 2,
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(6),
                HourlyRate = 20.00m,
                DressCode = "Chemise noire, pantalon noir.",
            },
            new Position
            {
                Id = WelcomePositionId,
                AgencyId = agencyId,
                EventId = EventId,
                Label = "Accueil",
                Headcount = 1,
                StartsAt = startsAt.AddHours(-1),
                EndsAt = startsAt.AddHours(3),
                HourlyRate = 18.00m,
            });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed de dev : agence {AgencyId}, 1 compte d'agence, {ContractorCount} prestataires, 1 événement.",
            agencyId,
            Contractors.Length);
    }
}
