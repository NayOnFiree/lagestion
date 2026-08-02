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
        // avoir un compte dans deux agences distinctes.
        builder.HasIndex(u => new { u.AgencyId, u.Email }).IsUnique();
    }
}
