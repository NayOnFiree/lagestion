using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaGestion.Api.Infrastructure.Configurations;

public sealed class ContractorConfiguration : EntityConfiguration<Contractor>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Contractor> builder)
    {
        builder.Property(c => c.LegalStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Siret).HasMaxLength(14);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.Iban).HasMaxLength(34);
        builder.Property(c => c.DefaultHourlyRate).HasPrecision(10, 2);
        builder.Property(c => c.BaseCity).HasMaxLength(150);
        builder.Property(c => c.Score).HasPrecision(4, 2);
        builder.Property(c => c.InvoicePrefix).HasMaxLength(20);
        builder.Property(c => c.NextInvoiceSequence).HasDefaultValue(1);

        // Un compte donne accès à au plus une fiche prestataire.
        builder.HasIndex(c => c.UserId).IsUnique();

        builder
            .HasOne(c => c.User)
            .WithMany()
            .HasPrincipalKey(u => new { u.Id, u.AgencyId })
            .HasForeignKey(c => new { c.UserId, c.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SkillConfiguration : EntityConfiguration<Skill>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Skill> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(s => new { s.AgencyId, s.Name }).IsUnique();
    }
}

public sealed class ContractorSkillConfiguration : EntityConfiguration<ContractorSkill>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ContractorSkill> builder)
    {
        builder.HasIndex(cs => new { cs.ContractorId, cs.SkillId }).IsUnique();

        builder
            .HasOne(cs => cs.Contractor)
            .WithMany(c => c.Skills)
            .HasPrincipalKey(c => new { c.Id, c.AgencyId })
            .HasForeignKey(cs => new { cs.ContractorId, cs.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(cs => cs.Skill)
            .WithMany(s => s.Contractors)
            .HasPrincipalKey(s => new { s.Id, s.AgencyId })
            .HasForeignKey(cs => new { cs.SkillId, cs.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DocumentConfiguration : EntityConfiguration<Document>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.FileKey).HasMaxLength(500).IsRequired();
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.ReviewNote).HasMaxLength(1000);

        // Le balayage des pièces qui expirent est la requête la plus fréquente.
        builder.HasIndex(d => new { d.AgencyId, d.ExpiresAt });

        builder
            .HasOne(d => d.Contractor)
            .WithMany(c => c.Documents)
            .HasPrincipalKey(c => new { c.Id, c.AgencyId })
            .HasForeignKey(d => new { d.ContractorId, d.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);

        // Référence facultative : PostgreSQL n'applique pas une clé étrangère
        // composite dont une colonne est nulle, ce qui est exactement le
        // comportement voulu tant qu'aucun relecteur n'est désigné.
        builder
            .HasOne(d => d.ReviewedBy)
            .WithMany()
            .HasPrincipalKey(u => new { u.Id, u.AgencyId })
            .HasForeignKey(d => new { d.ReviewedByUserId, d.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AvailabilityConfiguration : EntityConfiguration<Availability>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Availability> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(a => new { a.ContractorId, a.Date });

        // Deux déclarations ne peuvent pas partager le même début sur une même
        // date. Les recouvrements partiels sont refusés côté application : la
        // base ne sait pas comparer des intervalles sans extension.
        builder.HasIndex(a => new { a.ContractorId, a.Date, a.StartsAt }).IsUnique();

        builder
            .HasOne(a => a.Contractor)
            .WithMany()
            .HasPrincipalKey(c => new { c.Id, c.AgencyId })
            .HasForeignKey(a => new { a.ContractorId, a.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
