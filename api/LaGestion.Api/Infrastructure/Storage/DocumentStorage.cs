using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace LaGestion.Api.Infrastructure.Storage;

/// <summary>Emplacement des fichiers déposés.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Racine du stockage sur disque. Relative au répertoire courant si elle
    /// n'est pas absolue. Hors dépôt et ignorée par git.
    /// </summary>
    [Required]
    public string RootPath { get; set; } = "storage";

    /// <summary>Taille maximale acceptée pour une pièce.</summary>
    [Range(1, 50 * 1024 * 1024)]
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Durée de validité d'un lien de téléchargement signé.</summary>
    [Range(10, 600)]
    public int LinkLifetimeSeconds { get; set; } = 120;
}

/// <summary>Fichier déposé, tel que le stockage le restitue.</summary>
public sealed record StoredFile(Stream Content, string ContentType, string FileName);

/// <summary>
/// Stockage des pièces justificatives.
///
/// L'implémentation actuelle écrit sur disque ; une implémentation
/// S3-compatible prendra sa place en production sans que le reste du code
/// change, puisque seule la clé circule.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>Écrit le contenu et renvoie la clé sous laquelle le relire.</summary>
    Task<string> SaveAsync(Guid agencyId, Guid contractorId, string extension, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// Types acceptés. Le <c>Content-Type</c> annoncé par le client n'est jamais
/// cru sur parole : c'est la signature en octets qui décide.
/// </summary>
public static class AcceptedFileTypes
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Reconnaît le type réel d'après les premiers octets. Renvoie null si le
    /// contenu n'est pas d'un type accepté.
    /// </summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfMagic))
        {
            return ("application/pdf", ".pdf");
        }

        if (header.StartsWith(JpegMagic))
        {
            return ("image/jpeg", ".jpg");
        }

        if (header.StartsWith(PngMagic))
        {
            return ("image/png", ".png");
        }

        return null;
    }

    public const string HumanReadableList = "PDF, JPEG ou PNG";
}

/// <summary>Stockage sur disque local, pour le développement.</summary>
public sealed partial class LocalDiskDocumentStorage : IDocumentStorage
{
    private readonly string _rootPath;

    public LocalDiskDocumentStorage(Microsoft.Extensions.Options.IOptions<StorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>
    /// Forme imposée aux clés : trois identifiants hexadécimaux et une
    /// extension connue. Aucune clé venant de l'extérieur ne peut donc
    /// désigner un chemin hors de la racine.
    /// </summary>
    [GeneratedRegex(@"^[0-9a-f]{32}/[0-9a-f]{32}/[0-9a-f]{32}\.(pdf|jpg|png)$")]
    private static partial Regex KeyPattern { get; }

    public static bool IsValidKey(string key) => KeyPattern.IsMatch(key);

    public async Task<string> SaveAsync(
        Guid agencyId,
        Guid contractorId,
        string extension,
        Stream content,
        CancellationToken cancellationToken)
    {
        var key = $"{agencyId:N}/{contractorId:N}/{Guid.CreateVersion7():N}{extension}";
        var path = ResolveOrThrow(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);

        return key;
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        if (!IsValidKey(key))
        {
            return Task.FromResult<Stream?>(null);
        }

        var path = ResolveOrThrow(key);

        return Task.FromResult<Stream?>(
            File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (IsValidKey(key))
        {
            File.Delete(ResolveOrThrow(key));
        }

        return Task.CompletedTask;
    }

    private string ResolveOrThrow(string key)
    {
        if (!IsValidKey(key))
        {
            throw new ArgumentException($"Clé de stockage invalide : « {key} ».", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_rootPath, key));

        // Ceinture et bretelles : même si le motif changeait, on ne sort pas
        // de la racine.
        if (!path.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Clé de stockage hors racine : « {key} ».", nameof(key));
        }

        return path;
    }
}
