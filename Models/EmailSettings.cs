using System.IO;
using System.Text.Json;

namespace MahApps.Metro.Models;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string SenderPassword { get; set; } = "";

    public static EmailSettings Load()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "EmailSettings.json");

        if (!File.Exists(path))
            return new EmailSettings();

        string json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<EmailSettings>(json)
            ?? new EmailSettings();
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        !string.IsNullOrWhiteSpace(SenderEmail) &&
        !string.IsNullOrWhiteSpace(SenderPassword);
}