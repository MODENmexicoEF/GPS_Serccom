using MahApps.Metro.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MahApps.Metro.Services
{
    public class DatabaseService
    {
        private readonly DbContextOptions<EmailDbContext> _dbOptions;

        public DatabaseService(DbContextOptions<EmailDbContext> dbOptions)
        {
            _dbOptions = dbOptions;
            EnsureDatabaseCreated();
        }

        // Asegura que la base de datos y las tablas existan automáticamente al arrancar
        private void EnsureDatabaseCreated()
        {
            using var db = new EmailDbContext(_dbOptions);
            db.Database.EnsureCreated();
        }

        // --- GESTIÓN DE DESTINATARIOS ---

        public async Task<List<EmailRecipient>> ObtenerDestinatariosActivosAsync()
        {
            using var db = new EmailDbContext(_dbOptions);
            return await db.EmailRecipients
                .Where(x => x.IsActive)
                .ToListAsync();
        }
        public async Task<List<EmailRecipient>> ObtenerTodosLosDestinatariosAsync()
        {
            using var db = new EmailDbContext(_dbOptions);
            return await db.EmailRecipients.ToListAsync();
        }
        public async Task AgregarDestinatarioAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            using var db = new EmailDbContext(_dbOptions);
            var existe = await db.EmailRecipients.AnyAsync(x => x.Email.ToLower() == email.ToLower());
            if (!existe)
            {
                db.EmailRecipients.Add(new EmailRecipient { Email = email.Trim(), IsActive = true });
                await db.SaveChangesAsync();
            }
        }

        public async Task CambiarEstadoDestinatarioAsync(int id, bool isActive)
        {
            using var db = new EmailDbContext(_dbOptions);
            var recipient = await db.EmailRecipients.FindAsync(id);
            if (recipient != null)
            {
                recipient.IsActive = isActive;
                await db.SaveChangesAsync();
            }
        }

        // --- REGISTRO DE HISTORIAL (LOGS) ---

        public async Task RegistrarLogAsync(string nombreProceso, string processId, string estado, string detalle)
        {
            try
            {
                using var db = new EmailDbContext(_dbOptions);
                db.ProcesoLogs.Add(new ProcesoLog
                {
                    NombreProceso = nombreProceso,
                    ProcessId = processId,
                    Estado = estado,
                    FechaHora = DateTime.Now,
                    Detalle = detalle
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar log en DB: {ex.Message}");
            }
        }
        public async Task EliminarDestinatarioAsync(int id)
        {
            using var db = new EmailDbContext(_dbOptions);
            var recipient = await db.EmailRecipients.FindAsync(id);
            if (recipient != null)
            {
                db.EmailRecipients.Remove(recipient);
                await db.SaveChangesAsync();
            }
        }
    }
}