using System.Text;
using System.Text.Json;
using LaGestion.Api.Domain;
using LaGestion.Api.Features.Auth;
using LaGestion.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

const string FrontsCorsPolicy = "fronts";

// --- Multi-tenant ----------------------------------------------------------
// Agence du contexte courant, sur laquelle tous les accès sont filtrés. Elle
// vient du claim signé porté par le jeton d'accès, jamais du client.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAgencyContext, ClaimsAgencyContext>();

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
// AllowCredentials est indispensable au cookie de rafraîchissement, et impose
// des origines explicites : le joker est interdit avec des identifiants.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(FrontsCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// --- Authentification ------------------------------------------------------
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AgencyDbContextFactory>();
builder.Services.AddScoped<TokenService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Les noms de claims restent ceux du jeton : pas de traduction vers
        // les URI WS-Federation, qui rendent les jetons illisibles.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = LaGestionClaims.Role,
        };
    });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy("contractor", policy => policy.RequireRole(nameof(UserRole.Contractor)))
    .AddPolicy("admin", policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Owner)))
    .AddPolicy("owner", policy => policy.RequireRole(nameof(UserRole.Owner)));

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
