using LaGestion.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Jeu de données minimal pour travailler en local : une agence, un compte
/// d'agence, trois prestataires, un événement et ses deux postes.
///
/// Idempotent, comme <c>scripts/init-db.sql</c> : si l'agence de dév est déjà
/// là, le seed ne fait rien. Les identifiants sont fixes, pour que les URLs et
/// les captures d'écran restent stables d'une remise à zéro à l'autre.
///
/// S'exécute hors requête HTTP, donc avec son propre contexte de données posé
/// sur une agence explicite : il ne peut rien écrire ailleurs.
///
/// Ne s'exécute qu'en environnement de développement.
/// </summary>
public sealed class DevelopmentSeeder(
    AgencyDbContextFactory contextFactory,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider,
    ILogger<DevelopmentSeeder> logger)
{
    /// <summary>Agence du jeu de démonstration, saisie comme « demo » à la connexion.</summary>
    public static readonly Guid DemoAgencyId = new("0198f000-0000-7000-8000-000000000001");

    /// <summary>
    /// Mot de passe commun à tous les comptes du seed. Documenté dans le
    /// README : c'est un jeu de démonstration local, pas un secret.
    /// </summary>
    public const string DemoPassword = "LaGestion!2026";

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
    private static readonly Guid BarAssignmentId = new("0198f000-0000-7000-8000-000000000040");
    private static readonly Guid WelcomeAssignmentId = new("0198f000-0000-7000-8000-000000000041");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = contextFactory.CreateFor(DemoAgencyId);

        if (await db.Agencies.AnyAsync(a => a.Id == DemoAgencyId, cancellationToken))
        {
            logger.LogInformation("Seed de dév : agence {AgencyId} déjà présente, rien à faire.", DemoAgencyId);
            return;
        }

        db.Agencies.Add(new Agency
        {
            Id = DemoAgencyId,
            Name = "Agence de démonstration",
            Slug = "demo",
            Siret = "12345678900012",
            Address = "12 rue des Fêtes, 44000 Nantes",
            ContactEmail = "contact@agence-demo.test",
            ContactPhone = "0240000000",
        });

        AddUser(db, AdminUserId, "admin@agence-demo.test", UserRole.Admin, "Sacha", "Dumont", "0600000000");

        foreach (var (userId, contractorId, firstName, lastName, email, rate) in Contractors)
        {
            AddUser(db, userId, email, UserRole.Contractor, firstName, lastName, phone: null);

            db.Contractors.Add(new Contractor
            {
                Id = contractorId,
                AgencyId = DemoAgencyId,
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
            AgencyId = DemoAgencyId,
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
                AgencyId = DemoAgencyId,
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
                AgencyId = DemoAgencyId,
                EventId = EventId,
                Label = "Accueil",
                Headcount = 1,
                StartsAt = startsAt.AddHours(-1),
                EndsAt = startsAt.AddHours(3),
                HourlyRate = 18.00m,
            });

        // Deux missions confirmées : sans elles le calendrier n'aurait que
        // deux états sur trois et les compteurs du mois resteraient à zéro.
        var proposedAt = timeProvider.GetUtcNow().AddDays(-3);

        db.Assignments.AddRange(
            new Assignment
            {
                Id = BarAssignmentId,
                AgencyId = DemoAgencyId,
                PositionId = BarPositionId,
                ContractorId = Contractors[0].ContractorId,
                Status = AssignmentStatus.Confirmed,
                ProposedAt = proposedAt,
                ResponseDeadline = proposedAt.AddDays(2),
                RespondedAt = proposedAt.AddHours(5),
            },
            new Assignment
            {
                Id = WelcomeAssignmentId,
                AgencyId = DemoAgencyId,
                PositionId = WelcomePositionId,
                ContractorId = Contractors[1].ContractorId,
                Status = AssignmentStatus.Confirmed,
                ProposedAt = proposedAt,
                ResponseDeadline = proposedAt.AddDays(2),
                RespondedAt = proposedAt.AddHours(9),
            });

        // Quelques disponibilités déclarées, pour que le calendrier ne soit pas
        // vide à la première ouverture.
        var firstOfMonth = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1);

        foreach (var offset in new[] { 0, 1, 2, 6, 7, 8 })
        {
            db.Availabilities.Add(new Availability
            {
                AgencyId = DemoAgencyId,
                ContractorId = Contractors[0].ContractorId,
                Date = firstOfMonth.AddDays(offset),
                Status = offset % 3 == 2 ? AvailabilityStatus.Unavailable : AvailabilityStatus.Available,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed de dév : agence « demo », 1 compte d'agence et {ContractorCount} prestataires, 1 événement, 2 missions confirmées. Mot de passe commun documenté dans le README.",
            Contractors.Length);
    }

    private void AddUser(
        LaGestionDbContext db,
        Guid id,
        string email,
        UserRole role,
        string firstName,
        string lastName,
        string? phone)
    {
        var user = new User
        {
            Id = id,
            AgencyId = DemoAgencyId,
            Email = email,
            PasswordHash = string.Empty,
            Role = role,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
        };

        user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);

        db.Users.Add(user);
    }
}
