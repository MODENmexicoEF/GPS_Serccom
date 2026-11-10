using MahApps.Metro.Models;
using Microsoft.EntityFrameworkCore;

namespace MahApps.Metro.Services
{
    public class EmailDbContext : DbContext
    {
        private readonly string _connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Database=GpsSerccomEmail;Trusted_Connection=True;TrustServerCertificate=True;";

        public EmailDbContext() { }

        public EmailDbContext(DbContextOptions<EmailDbContext> options)
            : base(options)
        {
        }

        public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();
        public DbSet<ProcesoLog> ProcesoLogs => Set<ProcesoLog>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(_connectionString);
            }
        }
    }
}