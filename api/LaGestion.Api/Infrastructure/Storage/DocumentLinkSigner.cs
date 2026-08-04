using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LaGestion.Api.Infrastructure.Storage;

/// <summary>
/// Signe et vérifie les liens de téléchargement à durée courte.
///
/// Le contenu d'une pièce ne peut pas voyager avec un en-tête
/// <c>Authorization</c> : un &lt;img&gt; ou un onglet ouvert par le navigateur
/// n'en pose pas. Le lien porte donc lui-même sa preuve d'autorisation, sous
/// forme d'une signature HMAC valable quelques minutes.
///
/// La clé de signature est dérivée de celle des jetons, avec une étiquette
/// distincte : un même secret à provisionner, mais deux clés indépendantes —
/// signer un lien ne permet pas de forger un jeton, et réciproquement.
/// </summary>
public sealed class DocumentLinkSigner
{
    private const string DerivationLabel = "lagestion/document-link/v1";

    private readonly byte[] _key;
    private readonly TimeProvider _timeProvider;
    private readonly int _lifetimeSeconds;

    public DocumentLinkSigner(
        IOptions<JwtOptions> jwtOptions,
        IOptions<StorageOptions> storageOptions,
        TimeProvider timeProvider)
    {
        _key = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(DerivationLabel),
            Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey));

        _timeProvider = timeProvider;
        _lifetimeSeconds = storageOptions.Value.LinkLifetimeSeconds;
    }

    /// <summary>Instant d'expiration du prochain lien émis.</summary>
    public DateTimeOffset NextExpiry => _timeProvider.GetUtcNow().AddSeconds(_lifetimeSeconds);

    public string Sign(string storageKey, DateTimeOffset expiresAt)
    {
        var payload = $"{storageKey}|{expiresAt.ToUnixTimeSeconds()}";
        var signature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexStringLower(signature);
    }

    /// <summary>Vérifie la signature puis l'expiration, dans cet ordre.</summary>
    public bool IsValid(string storageKey, long expiresAtUnix, string signature)
    {
        var expected = Sign(storageKey, DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix));

        // Comparaison à temps constant : une comparaison naïve laisse fuiter
        // la signature attendue, octet par octet.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature)))
        {
            return false;
        }

        return DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix) > _timeProvider.GetUtcNow();
    }
}
