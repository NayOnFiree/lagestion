using Microsoft.EntityFrameworkCore;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// DbContext racine de l'application. Aucune entité pour l'instant.
///
/// Convention multi-tenant : toute entité métier ajoutée ici portera un
/// <c>AgencyId</c> et tous les accès seront filtrés dessus (voir README).
/// </summary>
public class LaGestionDbContext(DbContextOptions<LaGestionDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
