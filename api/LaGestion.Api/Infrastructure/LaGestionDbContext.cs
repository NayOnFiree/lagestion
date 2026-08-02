using System.Reflection;
using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// DbContext racine de l'application.
///
/// Trois comportements sont appliqués automatiquement à toute entité portant
/// <see cref="IAgencyOwned"/>, sans qu'aucun code appelant n'ait à y penser :
/// filtre de requête global sur l'agence courante, affectation de l'agence à
/// l'insertion, et horodatage création / mise à jour.
/// </summary>
public class LaGestionDbContext : DbContext
{
    private readonly IAgencyContext _agencyContext;

    public LaGestionDbContext(DbContextOptions<LaGestionDbContext> options, IAgencyContext agencyContext)
        : base(options)
    {
        _agencyContext = agencyContext;
    }

    /// <summary>
    /// Agence courante, référencée par le filtre global. EF Core la transforme
    /// en paramètre de requête réévalué à chaque exécution.
    /// </summary>
    private Guid CurrentAgencyId => _agencyContext.AgencyId;

    // Racine du multi-tenant : non filtrée, c'est elle qui porte le filtre.
    public DbSet<Agency> Agencies => Set<Agency>();

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ContractorSkill> ContractorSkills => Set<ContractorSkill>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyAgencyQueryFilters(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAutomaticValues();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAutomaticValues();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // --- Multi-tenant ------------------------------------------------------

    private static readonly MethodInfo AgencyFilterMethod = typeof(LaGestionDbContext)
        .GetMethod(nameof(SetAgencyQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Pose le filtre d'agence sur chaque entité <see cref="IAgencyOwned"/>.
    /// Le filtre est appliqué type par type : une requête qui oublierait une
    /// jointure reste filtrée, contrairement à un filtre porté par le parent.
    /// </summary>
    private void ApplyAgencyQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.IsAssignableTo(typeof(IAgencyOwned)))
            {
                AgencyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void SetAgencyQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IAgencyOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.AgencyId == CurrentAgencyId);
    }

    /// <summary>
    /// Pose l'agence et les horodatages avant écriture.
    ///
    /// Une entité insérée avec l'agence d'un autre tenant est refusée : c'est
    /// une erreur de programmation, pas un cas à corriger silencieusement.
    /// </summary>
    private void ApplyAutomaticValues()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State is EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Property(nameof(Entity.CreatedAt)).IsModified = false;
            }
        }

        foreach (var entry in ChangeTracker.Entries<IAgencyOwned>())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.AgencyId == Guid.Empty:
                    entry.Entity.AgencyId = CurrentAgencyId;
                    break;

                case EntityState.Added when entry.Entity.AgencyId != CurrentAgencyId:
                    throw new InvalidOperationException(
                        $"Tentative d'insertion d'un(e) {entry.Entity.GetType().Name} sur l'agence " +
                        $"{entry.Entity.AgencyId} alors que l'agence courante est {CurrentAgencyId}.");

                case EntityState.Modified:
                    // L'agence d'une entité existante ne se réaffecte pas.
                    entry.Property(nameof(IAgencyOwned.AgencyId)).IsModified = false;
                    break;
            }
        }
    }

    // --- Nommage PostgreSQL ------------------------------------------------

    /// <summary>
    /// Traduit le modèle en snake_case : C# reste en PascalCase, la base reste
    /// en snake_case, sans dépendance supplémentaire.
    /// </summary>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        // Deux passes : les noms de contraintes sont dérivés des noms de tables
        // et de colonnes. Les renommer dans la même boucle donnerait des noms
        // mixtes selon l'ordre d'itération.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var key in entityType.GetKeys())
            {
                key.SetName(ToConstraintName(key.GetDefaultName()));
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToConstraintName(foreignKey.GetDefaultName()));
            }

            foreach (var index in entityType.GetIndexes())
            {
                index.SetDatabaseName(ToConstraintName(index.GetDefaultDatabaseName()));
            }
        }
    }

    /// <summary>
    /// Les noms de contraintes générés par EF sont déjà préfixés
    /// (<c>PK_</c>, <c>FK_</c>, <c>IX_</c>) : on abaisse le préfixe et on
    /// passe le reste en snake_case, d'où <c>fk_contractors_agencies_agency_id</c>.
    /// </summary>
    private static string? ToConstraintName(string? defaultName)
    {
        if (string.IsNullOrEmpty(defaultName))
        {
            return defaultName;
        }

        var parts = defaultName.Split('_');
        return string.Join('_', parts.Select((part, index) =>
            index == 0 ? part.ToLowerInvariant() : ToSnakeCase(part)));
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new System.Text.StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (char.IsUpper(current))
            {
                // Coupe avant une majuscule précédée d'une minuscule (AgencyId
                // → agency_id) ou débutant un mot après un acronyme
                // (PDFKey → pdf_key).
                var previous = i > 0 ? name[i - 1] : '\0';
                var next = i + 1 < name.Length ? name[i + 1] : '\0';

                var startsNewWord = i > 0
                    && previous != '_'
                    && (!char.IsUpper(previous) || (char.IsUpper(previous) && char.IsLower(next)));

                if (startsNewWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
