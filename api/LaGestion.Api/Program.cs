using System.Text.Json;
using LaGestion.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string FrontsCorsPolicy = "fronts";

// --- Multi-tenant ----------------------------------------------------------
// Agence du contexte courant, sur laquelle tous les accès sont filtrés.
// Implémentation provisoire lisant la configuration : elle sera remplacée en
// phase 2 par la lecture du claim d'agence du JWT. Dans les deux cas la valeur
// ne vient jamais du client.
builder.Services.AddScoped<IAgencyContext, ConfigurationAgencyContext>();

// --- Persistance -----------------------------------------------------------
// La chaîne de connexion vient de la configuration :
//   - appsettings.Development.json en local (identifiants de dev, non secrets)
//   - user-secrets / variables d'environnement partout ailleurs
builder.Services.AddDbContext<LaGestionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// --- MVC / sérialisation ---------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// --- Erreurs au format ProblemDetails (RFC 9457) ---------------------------
builder.Services.AddProblemDetails();

// --- CORS ------------------------------------------------------------------
// Origines des deux fronts, lues depuis Cors:AllowedOrigins.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(FrontsCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// --- OpenAPI ---------------------------------------------------------------
builder.Services.AddOpenApi();

// --- Développement ---------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<DevelopmentSeeder>();
}

var app = builder.Build();

// Toute exception non gérée et tout code d'erreur sortent en ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Document : /openapi/v1.json — UI : /swagger
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "LaGestion API v1");
        options.RoutePrefix = "swagger";
    });

    // Jeu de données de dev. Idempotent : ne fait rien si l'agence existe.
    // Le schéma reste géré par les migrations, le seed ne crée aucune table.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
}
else
{
    // En dev on sert en HTTP simple : les fronts tapent l'API sans certificat à approuver.
    app.UseHttpsRedirection();
}

app.UseCors(FrontsCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
