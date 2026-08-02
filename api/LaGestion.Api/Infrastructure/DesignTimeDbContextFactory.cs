using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LaGestion.Api.Infrastructure;

/// <summary>
/// Contexte utilisé par les outils <c>dotnet ef</c>.
///
/// Sans cette fabrique, les outils exécutent <c>Program.cs</c> pour retrouver
/// le hôte — et démarrent réellement l'API, qui reste ensuite à écouter sur
/// son port et à verrouiller ses binaires. Ici, ils ne construisent que le
/// modèle.
///
/// L'agence est laissée vide : à la génération d'une migration, seul le
/// schéma compte, aucune requête n'est exécutée.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LaGestionDbContext>
{
    public LaGestionDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<LaGestionDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Postgres"))
            .Options;

        return new LaGestionDbContext(options, new FixedAgencyContext(Guid.Empty));
    }
}
