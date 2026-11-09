using MahApps.Metro.Models;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MahApps.Metro.Services;

public class EmailService
{
    private readonly EmailSettings _settings;
    private readonly DbContextOptions<EmailDbContext> _dbOptions;

    public EmailService(
        EmailSettings settings,
        DbContextOptions<EmailDbContext> dbOptions)
    {
        _settings = settings;
        _dbOptions = dbOptions;
    }

    public async Task SendProcessAlertAsync(

        string processName,
        string processId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
            return;
        await using var db = new EmailDbContext(_dbOptions);

        var recipients = await db.EmailRecipients
            .Where(x => x.IsActive)
            .Select(x => x.Email)
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
            return;

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _settings.SenderName,
            _settings.SenderEmail));

        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = $"Alerta de proceso: {processName}";

        message.Body = new TextPart("plain")
        {
            Text =
                $"Se ha detectado la detención de un proceso.\n\n" +
                $"Proceso: {processName}\n" +
                $"PID: {processId}\n" +
                $"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n\n" +
                "Mensaje generado automáticamente por GPS Serccom."
        };

        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(
            _settings.SmtpHost,
            _settings.SmtpPort,
            SecureSocketOptions.StartTls,
            cancellationToken);

        await smtp.AuthenticateAsync(
            _settings.SenderEmail,
            _settings.SenderPassword,
            cancellationToken);

        await smtp.SendAsync(message, cancellationToken);

        await smtp.DisconnectAsync(
            true,
            cancellationToken);
    }
}