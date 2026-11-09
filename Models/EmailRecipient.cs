namespace MahApps.Metro.Models;

public class EmailRecipient
{
    public int Id { get; set; }

    public string Email { get; set; } = "";

    public bool IsActive { get; set; } = true;
}