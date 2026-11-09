using MahApps.Metro.Models;
using Microsoft.EntityFrameworkCore;

namespace MahApps.Metro.Services;

public class EmailDbContext : DbContext
{
    private string connectionString =
    @"Server=(localdb)\MSSQLLocalDB;
      Database=GpsSerccomEmail;
      Trusted_Connection=True;
      TrustServerCertificate=True;";

    public EmailDbContext(
        DbContextOptions<EmailDbContext> options)
        : base(options)
    {
    }

    public DbSet<EmailRecipient> EmailRecipients =>
        Set<EmailRecipient>();
}