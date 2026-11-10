using MahApps.Metro.Models;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Threading;
using System.Threading.Tasks;

namespace MahApps.Metro.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;
        private readonly DatabaseService _databaseService;

        public EmailService(EmailSettings settings, DatabaseService databaseService)
        {
            _settings = settings;
            _databaseService = databaseService;
        }

        public async Task SendProcessAlertAsync(
            string processName,
            string processId,
            string estadoProceso,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.IsConfigured)
                return;

            // Obtenemos los destinatarios directamente de la LocalDB usando nuestro servicio
            var recipientsList = await _databaseService.ObtenerDestinatariosActivosAsync();
            if (recipientsList.Count == 0)
            {
                // Registramos en la BD que la alerta no se pudo enviar por falta de destinatarios activos
                await _databaseService.RegistrarLogAsync(
                    processName,
                    processId,
                    estadoProceso,
                    "Fallo al enviar correo: No existen destinatarios activos configurados en el sistema.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));

            foreach (var recipient in recipientsList)
            {
                message.To.Add(MailboxAddress.Parse(recipient.Email));
            }

            message.Subject = $"Alerta GPS Serccom: Proceso {processName} ({estadoProceso})";

            message.Body = new TextPart("plain")
            {
                Text =
                    $"Se ha registrado un cambio de estado en el proceso monitorizado.\n\n" +
                    $"Proceso: {processName}\n" +
                    $"PID: {processId}\n" +
                    $"Estado: {estadoProceso}\n" +
                    $"Fecha y Hora: {System.DateTime.Now:dd/MM/yyyy HH:mm:ss}\n\n" +
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
            await smtp.DisconnectAsync(true, cancellationToken);
        }
    }
}