using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaGestion.Api.Infrastructure.Configurations;

/// <summary>
/// Base commune des configurations : identifiant, horodatage, et pour les
/// entités métier la colonne <c>agency_id</c> et son index.
///
/// Les noms de tables et de colonnes ne sont pas déclarés ici : le passage en
/// snake_case est fait par convention dans <see cref="LaGestionDbContext"/>.
/// </summary>
public abstract class EntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        if (typeof(IAgencyOwned).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(IAgencyOwned.AgencyId)).IsRequired();
            builder.HasIndex(nameof(IAgencyOwned.AgencyId));

            // Clé alternative servant de cible aux clés étrangères composites
            // des entités filles : PostgreSQL refuse alors physiquement qu'une
            // ligne référence un parent d'une autre agence. Redondante avec la
            // clé primaire, mais c'est le prix de la contrainte.
            builder.HasAlternateKey([nameof(Entity.Id), nameof(IAgencyOwned.AgencyId)]);

            // Relation sans navigation : l'agence est un cadre, pas quelque
            // chose qu'on remonte depuis chaque entité.
            builder
                .HasOne<Agency>()
                .WithMany()
                .HasForeignKey(nameof(IAgencyOwned.AgencyId))
                .OnDelete(DeleteBehavior.Restrict);
        }

        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}

public sealed class AgencyConfiguration : EntityConfiguration<Agency>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Agency> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();

        // Saisi à la connexion, donc unique toutes agences confondues.
        builder.Property(a => a.Slug).HasMaxLength(50).IsRequired();
        builder.HasIndex(a => a.Slug).IsUnique();

        builder.Property(a => a.Siret).HasMaxLength(14);
        builder.Property(a => a.Address).HasMaxLength(500);
        builder.Property(a => a.ContactEmail).HasMaxLength(320);
        builder.Property(a => a.ContactPhone).HasMaxLength(30);
    }
}

public sealed class UserConfiguration : EntityConfiguration<User>
{
    protected override void ConfigureEntity(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(30);

        // Une adresse ne sert qu'une fois par agence : le même individu peut
        // avoir un compte dans deux agences distinctes. C'est le slug d'agence
        // saisi à la connexion qui lève l'ambiguïté.
        builder.HasIndex(u => new { u.AgencyId, u.Email }).IsUnique();
    }
}

public sealed class RefreshTokenConfiguration : EntityConfiguration<RefreshToken>
{
    protected override void ConfigureEntity(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

        // Point d'entrée du rafraîchissement : on cherche par condensat.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Révocation en masse de la chaîne d'un utilisateur.
        builder.HasIndex(t => new { t.UserId, t.RevokedAt });

        builder
            .HasOne(t => t.User)
            .WithMany()
            .HasPrincipalKey(u => new { u.Id, u.AgencyId })
            .HasForeignKey(t => new { t.UserId, t.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
