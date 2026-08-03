using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaGestion.Api.Infrastructure.Configurations;

public sealed class EventConfiguration : EntityConfiguration<Event>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Event> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ClientName).HasMaxLength(200);
        builder.Property(e => e.Address).HasMaxLength(500);
        // Valeur par défaut explicite : sans elle, la migration qui ajoute la
        // colonne remplit les lignes existantes avec une chaîne vide, qui ne
        // se reconvertit en aucune valeur de l'énumération.
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(EventStatus.Draft)
            .IsRequired();

        // Le back-office liste les événements par période.
        builder.HasIndex(e => new { e.AgencyId, e.StartsAt });
    }
}

public sealed class PositionConfiguration : EntityConfiguration<Position>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Position> builder)
    {
        builder.Property(p => p.Label).HasMaxLength(150).IsRequired();
        builder.Property(p => p.HourlyRate).HasPrecision(10, 2).IsRequired();
        builder.Property(p => p.DressCode).HasMaxLength(300);

        builder.HasIndex(p => p.EventId);

        builder
            .HasOne(p => p.Event)
            .WithMany(e => e.Positions)
            .HasPrincipalKey(e => new { e.Id, e.AgencyId })
            .HasForeignKey(p => new { p.EventId, p.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AssignmentConfiguration : EntityConfiguration<Assignment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Assignment> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Un prestataire n'est sollicité qu'une fois par poste.
        builder.HasIndex(a => new { a.PositionId, a.ContractorId }).IsUnique();

        // « Mes missions », côté prestataire.
        builder.HasIndex(a => new { a.ContractorId, a.Status });

        // Aucune suppression physique : une proposition annulée passe en
        // statut Cancelled et reste tracée.
        builder
            .HasOne(a => a.Position)
            .WithMany(p => p.Assignments)
            .HasPrincipalKey(p => new { p.Id, p.AgencyId })
            .HasForeignKey(a => new { a.PositionId, a.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(a => a.Contractor)
            .WithMany()
            .HasPrincipalKey(c => new { c.Id, c.AgencyId })
            .HasForeignKey(a => new { a.ContractorId, a.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TimesheetConfiguration : EntityConfiguration<Timesheet>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Timesheet> builder)
    {
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.PlannedHours).HasPrecision(6, 2).IsRequired();
        builder.Property(t => t.ActualHours).HasPrecision(6, 2);

        // Une prestation, un relevé d'heures.
        builder.HasIndex(t => t.AssignmentId).IsUnique();

        builder
            .HasOne(t => t.Assignment)
            .WithOne(a => a.Timesheet)
            .HasPrincipalKey<Assignment>(a => new { a.Id, a.AgencyId })
            .HasForeignKey<Timesheet>(t => new { t.AssignmentId, t.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(t => t.ValidatedBy)
            .WithMany()
            .HasPrincipalKey(u => new { u.Id, u.AgencyId })
            .HasForeignKey(t => new { t.ValidatedByUserId, t.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
