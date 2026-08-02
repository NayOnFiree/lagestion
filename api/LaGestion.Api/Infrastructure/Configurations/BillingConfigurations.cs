using LaGestion.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaGestion.Api.Infrastructure.Configurations;

public sealed class InvoiceConfiguration : EntityConfiguration<Invoice>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Number).HasMaxLength(50);
        builder.Property(i => i.TotalAmount).HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.PdfKey).HasMaxLength(500);

        // Numérotation propre à chaque prestataire, continue et sans trou.
        // Le rang reste nul tant que la facture est un brouillon ; PostgreSQL
        // autorise plusieurs NULL sous une contrainte d'unicité, les
        // brouillons ne se gênent donc pas entre eux.
        builder.HasIndex(i => new { i.ContractorId, i.SequenceIndex }).IsUnique();

        builder.HasIndex(i => new { i.AgencyId, i.Status });

        // Aucune suppression physique : une facture annulée reste en base.
        builder
            .HasOne(i => i.Contractor)
            .WithMany()
            .HasPrincipalKey(c => new { c.Id, c.AgencyId })
            .HasForeignKey(i => new { i.ContractorId, i.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceLineConfiguration : EntityConfiguration<InvoiceLine>
{
    protected override void ConfigureEntity(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.Property(l => l.Label).HasMaxLength(300).IsRequired();
        builder.Property(l => l.Hours).HasPrecision(6, 2).IsRequired();
        builder.Property(l => l.UnitRate).HasPrecision(10, 2).IsRequired();
        builder.Property(l => l.Amount).HasPrecision(12, 2).IsRequired();

        builder.HasIndex(l => l.InvoiceId);

        builder
            .HasOne(l => l.Invoice)
            .WithMany(i => i.Lines)
            .HasPrincipalKey(i => new { i.Id, i.AgencyId })
            .HasForeignKey(l => new { l.InvoiceId, l.AgencyId })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(l => l.Assignment)
            .WithMany()
            .HasPrincipalKey(a => new { a.Id, a.AgencyId })
            .HasForeignKey(l => new { l.AssignmentId, l.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : EntityConfiguration<Notification>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Template).HasMaxLength(100).IsRequired();
        builder.Property(n => n.Payload).HasColumnType("jsonb");

        // File d'envoi : on balaie les notifications en attente.
        builder.HasIndex(n => new { n.Status, n.CreatedAt });

        builder
            .HasOne(n => n.User)
            .WithMany()
            .HasPrincipalKey(u => new { u.Id, u.AgencyId })
            .HasForeignKey(n => new { n.UserId, n.AgencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
