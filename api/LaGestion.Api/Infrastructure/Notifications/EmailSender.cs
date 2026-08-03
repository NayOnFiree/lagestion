using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace LaGestion.Api.Infrastructure.Notifications;

/// <summary>Configuration d'envoi des mails.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Adresse d'expédition affichée.</summary>
    [Required]
    public string FromAddress { get; set; } = "ne-pas-repondre@lagestion.local";

    [Required]
    public string FromName { get; set; } = "LaGestion";

    /// <summary>
    /// Serveur SMTP. Laissé vide, les mails sont écrits sur disque au lieu
    /// d'être envoyés : c'est le mode de développement.
    /// </summary>
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUser { get; set; }

    public string? SmtpPassword { get; set; }

    /// <summary>Répertoire des mails écrits sur disque, en l'absence de serveur SMTP.</summary>
    public string DropPath { get; set; } = "storage/mails";

    /// <summary>Adresse de base des fronts, pour les liens contenus dans les mails.</summary>
    public string ContractorAppUrl { get; set; } = "http://localhost:5173";

    public string AdminAppUrl { get; set; } = "http://localhost:5174";
}

/// <summary>Expédition d'un message.</summary>
public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string html, string text, CancellationToken cancellationToken);
}

/// <summary>
/// Expéditeur SMTP.
///
/// Volontairement agnostique du fournisseur : Brevo, Mailjet, OVH ou
/// Scaleway se configurent par hôte, port et identifiants, sans toucher au
/// code.
///
/// Sans hôte configuré, les messages sont écrits en fichiers .eml : en
/// développement, on veut relire ce qui serait parti, pas l'envoyer.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string recipient,
        string subject,
        string html,
        string text,
        CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;

        message.Body = new MultipartAlternative
        {
            new TextPart(TextFormat.Plain) { Text = text },
            new TextPart(TextFormat.Html) { Text = html },
        };

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            await DropToDiskAsync(message, cancellationToken);
            return;
        }

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.SmtpHost,
            _options.SmtpPort,
            SecureSocketOptions.StartTlsWhenAvailable,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            await client.AuthenticateAsync(
                _options.SmtpUser,
                _options.SmtpPassword ?? string.Empty,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    private async Task DropToDiskAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(_options.DropPath);
        Directory.CreateDirectory(directory);

        var safeSubject = string.Concat(
                (message.Subject ?? "message").Where(c => char.IsLetterOrDigit(c) || c == ' '))
            .Trim()
            .Replace(' ', '-');

        var path = Path.Combine(
            directory,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.CreateVersion7():N}-{safeSubject}.eml");

        await using var file = File.Create(path);
        await message.WriteToAsync(file, cancellationToken);

        logger.LogInformation("Aucun serveur SMTP configuré : mail écrit dans {Path}.", path);
    }
}
